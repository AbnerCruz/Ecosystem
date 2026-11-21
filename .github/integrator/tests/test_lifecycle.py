import importlib.util
from pathlib import Path
import unittest

spec = importlib.util.spec_from_file_location('lifecycle', Path(__file__).parents[1] / 'lifecycle.py')
m = importlib.util.module_from_spec(spec)
spec.loader.exec_module(m)

class LifecycleTests(unittest.TestCase):
    def test_increment_keeps_stage(self):
        for old, new in [('2.0.0-beta.55','2.0.0-beta.56'), ('1.2.3-alpha.9','1.2.3-alpha.10'), ('1.2.3','1.2.4'), ('0.0.1-dev.1000575','0.0.1-dev.1000576'), ('1.0.0-beta','1.0.0-beta.1')]:
            self.assertEqual(m.increment(old), new)
    def test_invalid_versions_fail_closed(self):
        for v in ['foo', '1.2', '1.0.0-beta;echo bad', '1.0.0-1']:
            with self.assertRaises(ValueError): m.increment(v)
    def test_who_waits_for_the_owner_is_the_policy_alone(self):
        # ADD-0035: governance, ADRs and agent instructions integrate when green; only the 'extreme' class of the main's
        # integration-policy.json (the gate itself, signing keys) waits for the owner, and Check.cs applies it.
        # The release step adds only a stage change; it keeps no second list of paths (NN-001).
        import tempfile
        from unittest.mock import patch
        self.assertFalse(hasattr(m, 'protected'))
        cfg = m.config()['urbe']; original = (m.ROOT / cfg['versionFile']).read_text()
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp); path = root / cfg['versionFile']; path.parent.mkdir(parents=True); path.write_text(original)
            with patch.object(m, 'git', return_value=original):
                _, owner = m.prepare(root, 'base', 'head', ['apps/urbe/csharp/src/Test.cs', 'docs/governance/decisions.json', 'docs/adr/0051-x.md', 'AGENTS.md'])
            self.assertFalse(owner)
    def test_review_bound_to_head_and_owner(self):
        review = {'user':{'login':'AbnerCruz'},'commit_id':'abc','state':'APPROVED','submitted_at':'2026-11-15T12:00:00Z'}
        self.assertTrue(m.owner_approved([review], 'AbnerCruz', 'abc'))
        self.assertFalse(m.owner_approved([review], 'AbnerCruz', 'new'))
        self.assertFalse(m.owner_approved([review], 'other', 'abc'))
        self.assertFalse(m.owner_approved([review, dict(review, state='CHANGES_REQUESTED', submitted_at='2026-11-15T13:00:00Z')], 'AbnerCruz', 'abc'))
    def test_code_vs_docs_and_dependencies(self):
        self.assertEqual(m.affected(['apps/urbe/csharp/src/A.cs']), ['urbe'])
        self.assertEqual(m.affected(['apps/urbe/README.md', 'docs/governance/handoffs/a.json']), [])
        self.assertEqual(m.affected(['platform/text-inspection/src/A.cs']), ['hub', 'lunet2d'])
        self.assertEqual(m.affected(['apps/atlas-forge/src/main.js']), ['atlas-forge'])

if __name__ == '__main__': unittest.main()

class PrepareTests(unittest.TestCase):
    def test_number_is_prepared_before_commit_and_retry_is_deterministic(self):
        import tempfile
        import shutil
        from unittest.mock import patch
        cfg = m.config()['urbe']
        original = (m.ROOT / cfg['versionFile']).read_text()
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            path = root / cfg['versionFile']; path.parent.mkdir(parents=True); path.write_text(original)
            with patch.object(m, 'git', return_value=original):
                plans, owner = m.prepare(root, 'base', 'head', ['apps/urbe/csharp/src/Test.cs'])
            self.assertFalse(owner)
            old = m.read_version(m.ROOT, cfg)
            self.assertEqual(m.read_version(root, cfg), m.increment(old))
            self.assertEqual(plans[0]['head'], 'head')
            self.assertTrue((root / 'docs/distribution/release-queue/urbe' / (m.increment(old)+'.json')).exists())
            # A re-evaluation starts from the same PR tree and base, never from an already bumped candidate.
            path.write_text(original)
            with patch.object(m, 'git', return_value=original):
                again, _ = m.prepare(root, 'base', 'head', ['apps/urbe/csharp/src/Test.cs'])
            self.assertEqual(plans, again)
    def test_stage_change_requires_owner(self):
        import tempfile
        from unittest.mock import patch
        cfg = m.config()['urbe']; original = (m.ROOT / cfg['versionFile']).read_text()
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp); path = root / cfg['versionFile']; path.parent.mkdir(parents=True)
            path.write_text(original.replace(m.read_version(m.ROOT, cfg), '2.0.0'))
            with patch.object(m, 'git', return_value=original):
                _, owner = m.prepare(root, 'base', 'head', [cfg['versionFile']])
            self.assertTrue(owner)

class ChecksTests(unittest.TestCase):
    def test_additional_checks_cannot_be_ignored(self):
        good = {'id':1,'name':'unit','status':'completed','conclusion':'success'}
        self.assertTrue(m.checks_passed([good], []))
        self.assertFalse(m.checks_passed([], []))
        self.assertFalse(m.checks_passed([good, dict(good, id=2, conclusion='failure')], []))
        self.assertFalse(m.checks_passed([dict(good, status='in_progress')], []))
        self.assertFalse(m.checks_passed([good], [{'context':'external-ci','state':'pending'}]))
        self.assertTrue(m.checks_passed([good], [{'context':'ecosystem/integration','state':'pending'}]))

class DuplicateNamesTests(unittest.TestCase):
    def test_same_job_name_from_two_workflows_must_not_hide_failure(self):
        failed = {'id':1,'name':'test','status':'completed','conclusion':'failure','check_suite':{'id':10}}
        passed = {'id':2,'name':'test','status':'completed','conclusion':'success','check_suite':{'id':20}}
        self.assertFalse(m.checks_passed([failed, passed], []))

class IntegratorSuiteTests(unittest.TestCase):
    def test_integrator_does_not_wait_for_its_own_finish_job(self):
        own = {'id':1,'name':'Integrar','status':'in_progress','conclusion':None,'check_suite':{'id':10}}
        tests = {'id':2,'name':'test','status':'completed','conclusion':'success','check_suite':{'id':20}}
        self.assertTrue(m.checks_passed([own, tests], [], {10}))
        self.assertFalse(m.checks_passed([own, dict(tests, conclusion='failure')], [], {10}))
        self.assertFalse(m.checks_passed([own], [], {10}))

class WorkflowIdentityTests(unittest.TestCase):
    def test_only_verified_integrator_workflow_suites_are_excluded(self):
        from unittest.mock import patch
        checks = [
            {'check_suite':{'id':10}, 'app':{'slug':'github-actions'}, 'details_url':'https://github.com/owner/repo/actions/runs/1/job/10'},
            {'check_suite':{'id':20}, 'app':{'slug':'github-actions'}, 'details_url':'https://github.com/owner/repo/actions/runs/2/job/20'},
            {'check_suite':{'id':30}, 'app':{'slug':'third-party'}, 'details_url':'https://github.com/owner/repo/actions/runs/1/job/10'}]
        def api(endpoint):
            return {'workflow_id':42 if endpoint.endswith('/1') else 99, 'check_suite_id':10 if endpoint.endswith('/1') else 20}
        with patch.object(m, 'gh_json', side_effect=api):
            self.assertEqual(m.integrator_suites(checks, 'owner/repo', '1'), {10})
