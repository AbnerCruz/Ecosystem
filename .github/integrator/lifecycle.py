#!/usr/bin/env python3
"""Trusted git/JSON glue for ADD-0034. Never execute candidate-supplied commands."""
import argparse
import json
import re
import subprocess
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
CONFIG = 'docs/distribution/automatic-releases.json'


def git(*args, cwd=None):
    return subprocess.check_output(['git', *args], cwd=cwd or ROOT, text=True).strip()


def increment(version):
    match = re.fullmatch(r'(\d+)\.(\d+)\.(\d+)(?:-([A-Za-z][A-Za-z0-9-]*)(?:\.(\d+))?)?', version)
    if not match:
        raise ValueError(f'Unsupported canonical version: {version}')
    a, b, c, stage, n = match.groups()
    return f'{a}.{b}.{c}-{stage}.{int(n or 0)+1}' if stage else f'{a}.{b}.{int(c)+1}'


def stage(version):
    increment(version)  # validate first
    return version.split('-', 1)[1].split('.', 1)[0] if '-' in version else 'stable'


def protected(paths):
    return any(p in ('MANIFEST.md', 'AGENTS.md', 'ecosystem.json', CONFIG) or p.endswith('/AGENTS.md') or
               p.startswith(('.github/', 'docs/governance/', 'docs/contracts/', 'docs/adr/', 'tests/consistency/'))
               for p in paths if not p.startswith('docs/governance/handoffs/'))


def checks_passed(checks, statuses, ignored_suites=()):
    latest = {}
    for check in checks:
        if check.get('check_suite', {}).get('id') in ignored_suites: continue
        name = check.get('name')
        if not name: return False
        key = (check.get('app', {}).get('id'), check.get('check_suite', {}).get('id'), name)
        if key not in latest or check.get('id', 0) > latest[key].get('id', 0): latest[key] = check
    contexts = {}
    for status in statuses:
        name = status.get('context')
        if name == 'ecosystem/integration': continue
        if name not in contexts: contexts[name] = status
    return bool(latest or contexts) and all(c.get('status') == 'completed' and c.get('conclusion') in ('success', 'skipped', 'neutral') for c in latest.values()) and all(s.get('state') == 'success' for s in contexts.values())


def gh_json(endpoint):
    return json.loads(subprocess.check_output(['gh', 'api', endpoint], text=True))


def integrator_suites(checks, repo, current_run):
    # A job name is not an identity. Resolve Actions run metadata from GitHub and
    # compare workflow IDs and suite IDs; only this workflow's own evaluations are excluded.
    own = gh_json(f'repos/{repo}/actions/runs/{current_run}')
    workflow_id = own['workflow_id']
    ignored = {own['check_suite_id']}
    runs = {str(current_run): own}
    pattern = re.compile(r'https://github\.com/' + re.escape(repo) + r'/actions/runs/(\d+)(?:/|$)', re.I)
    for check in checks:
        if check.get('app', {}).get('slug') != 'github-actions': continue
        match = pattern.match(check.get('details_url') or '')
        if not match: continue
        run_id = match[1]
        if run_id not in runs: runs[run_id] = gh_json(f'repos/{repo}/actions/runs/{run_id}')
        run = runs[run_id]
        if run.get('workflow_id') == workflow_id and run.get('check_suite_id') == check.get('check_suite', {}).get('id'):
            ignored.add(run['check_suite_id'])
    return ignored


def owner_approved(reviews, owner, head):
    # A later changes-requested or dismissed review revokes an earlier approval.
    own = sorted((r for r in reviews if r.get('user', {}).get('login', '').lower() == owner.lower()
                  and r.get('state') in ('APPROVED', 'CHANGES_REQUESTED', 'DISMISSED')),
                 key=lambda r: (r.get('submitted_at') or '', r.get('id', 0)))
    return bool(own and own[-1].get('state') == 'APPROVED' and own[-1].get('commit_id') == head)


def config():
    return json.loads((ROOT / CONFIG).read_text())['products']


def affected(paths):
    result = set()
    for product, cfg in config().items():
        for p in paths:
            # Changes to tests/docs alone do not pretend the installed app changed.
            rel = p.removeprefix(f'apps/{product}/')
            if p.startswith(f'apps/{product}/') and not (
                rel.lower().endswith(('.md', '.png', '.jpg')) or
                rel.startswith(('docs/', 'tests/', '.github/')) or '/tests/' in rel):
                result.add(product)
            if p.startswith('platform/text-inspection/') and product in ('hub', 'lunet2d'):
                result.add(product)
            if p.startswith('platform/agent-runtime/') and product in ('urbe', 'lunet2d', 'ecosystem-ai'):
                result.add(product)
    return sorted(result)


def read_version(root, cfg):
    text = (root / cfg['versionFile']).read_text()
    if cfg['format'] == 'xml':
        return re.search(r'<UrbeBetaVersion>([^<]+)</UrbeBetaVersion>', text)[1]
    if cfg['format'] == 'json':
        return json.loads(text)['version']
    return text.strip()


def write_version(root, cfg, version):
    path = root / cfg['versionFile']
    text = path.read_text()
    if cfg['format'] == 'xml':
        text = re.sub(r'(<UrbeBetaVersion>)[^<]+', lambda m: m[1] + version, text)
        text = re.sub(r'(<UrbeBetaVersionCode>)(\d+)', lambda m: m[1] + str(int(m[2])+1), text)
    elif cfg['format'] == 'json':
        data = json.loads(text); data['version'] = version
        text = json.dumps(data, ensure_ascii=False, indent=2) + '\n'
        lock = path.with_name('package-lock.json')
        if lock.exists():
            data = json.loads(lock.read_text()); data['version'] = version
            if '' in data.get('packages', {}): data['packages']['']['version'] = version
            lock.write_text(json.dumps(data, ensure_ascii=False, indent=2) + '\n')
    else:
        text = version + '\n'
    path.write_text(text)


def prepare(work, base, head, files):
    plans = []
    needs_owner = protected(files)
    for product in affected(files):
        cfg = config()[product]  # Always trusted configuration, not the candidate's.
        before = git('show', f'{base}:{cfg["versionFile"]}')
        if cfg['format'] == 'xml': before = re.search(r'<UrbeBetaVersion>([^<]+)', before)[1]
        elif cfg['format'] == 'json': before = json.loads(before)['version']
        previous = before.strip()
        candidate = read_version(work, cfg)
        needs_owner |= stage(previous) != stage(candidate)
        # An explicitly raised number is kept, but a repeated/older same-stage version is bumped.
        if stage(previous) == stage(candidate):
            version = increment(previous) if candidate == previous else candidate
            def key(v): return tuple(int(x) if x.isdigit() else x for x in re.split(r'[.\-]', v))
            if key(version) <= key(previous): raise ValueError(f'{product}: version must increase')
        else:
            version = candidate  # only lands with an owner review for this head
        if cfg['format'] == 'xml':
            oldcode = int(re.search(r'<UrbeBetaVersionCode>(\d+)', git('show', f'{base}:{cfg["versionFile"]}'))[1])
            newcode = int(re.search(r'<UrbeBetaVersionCode>(\d+)', (work / cfg['versionFile']).read_text())[1])
            if version != candidate: write_version(work, cfg, version)
            if newcode <= oldcode:
                p = work / cfg['versionFile']; p.write_text(re.sub(r'(<UrbeBetaVersionCode>)\d+', lambda m:m[1]+str(oldcode+1), p.read_text()))
        elif version != candidate: write_version(work, cfg, version)
        if 'versionCodeFile' in cfg:
            codepath = work / cfg['versionCodeFile']
            oldcode = int(git('show', f'{base}:{cfg["versionCodeFile"]}'))
            codepath.write_text(str(max(oldcode + 1, int(codepath.read_text()))) + '\n')
        plan = {'product':product, 'version':version, 'head':head, 'workflow':cfg['workflow'], 'tag':cfg['tagPrefix']+version}
        queue = work / 'docs/distribution/release-queue' / product / (version + '.json')
        queue.parent.mkdir(parents=True, exist_ok=True)
        queue.write_text(json.dumps(plan, indent=2) + '\n')
        plans.append(plan)
    return plans, needs_owner


def main():
    p = argparse.ArgumentParser(); p.add_argument('command', choices=['prepare','authorize','dispatch','checks'])
    p.add_argument('--work'); p.add_argument('--base'); p.add_argument('--head'); p.add_argument('--files')
    p.add_argument('--integrator-run'); p.add_argument('--checks'); p.add_argument('--statuses'); p.add_argument('--reviews'); p.add_argument('--owner'); p.add_argument('--repo'); p.add_argument('--sha')
    a = p.parse_args()
    if a.command == 'checks':
        checks = json.loads(Path(a.checks).read_text())
        ignored = integrator_suites(checks, a.repo, a.integrator_run) if a.repo and a.integrator_run else set()
        raise SystemExit(0 if checks_passed(checks, json.loads(Path(a.statuses).read_text()), ignored) else 1)
    if a.command == 'authorize':
        approved = owner_approved(json.loads(Path(a.reviews).read_text()), a.owner, a.head)
        print('authorized=' + str(approved).lower()); print('authorization_reason=owner review bound to PR head' if approved else 'authorization_reason=owner review required for this head')
        return
    if a.command == 'prepare':
        plans, needs = prepare(Path(a.work), a.base, a.head, Path(a.files).read_text().splitlines())
        print('release_products=' + json.dumps([x['product'] for x in plans], separators=(',',':')))
        if needs: print('requires_owner=true')
        return
    # Immutable source and durable queue: both normal merges and GITHUB_TOKEN merges use this path.
    failures = []
    revision = a.sha or 'HEAD'
    files = git('ls-tree', '-r', '--name-only', revision, '--', 'docs/distribution/release-queue').splitlines()
    for filename in files:
        if not filename.endswith('.json'): continue
        plan = json.loads(git('show', f'{revision}:{filename}'))
        cfg = config()[plan['product']]
        view = subprocess.run(['gh','release','view',plan['tag'],'--repo',a.repo,'--json','assets'], capture_output=True, text=True)
        if view.returncode == 0 and len(json.loads(view.stdout)['assets']) >= cfg['minimumAssets']: continue
        sha = git('log','--first-parent','--diff-filter=A','--format=%H','-1',revision,'--',filename)
        if not sha: continue
        runs = subprocess.run(['gh','api',f'repos/{a.repo}/actions/workflows/{cfg["workflow"]}/runs?per_page=100'], capture_output=True, text=True)
        if runs.returncode != 0:
            failures.append(plan['tag'] + ': cannot inspect active runs'); continue
        active = json.loads(runs.stdout).get('workflow_runs', [])
        if any(sha in run.get('display_title', '') and run.get('status') != 'completed' for run in active): continue
        cmd = ['gh','workflow','run',cfg['workflow'],'--repo',a.repo,'--ref','main','-f',f'source_sha={sha}']
        if cfg['workflow'] == 'product-package-release.yml': cmd += ['-f',f'product={plan["product"]}']
        if subprocess.run(cmd).returncode: failures.append(plan['tag'])
    if failures: raise SystemExit('Pending release dispatches: ' + ', '.join(failures))

if __name__ == '__main__': main()
