"""Exercise offline project UI, then reopen through a private-root OS alias.

Runs only on the disposable emulator selected by smoke-android-preview.sh.
No provider, credentials, or third-party Python packages.
"""
import json
import re
import subprocess
import sys
import time
import xml.etree.ElementTree as ET
from pathlib import Path

adb, serial, package, activity = sys.argv[1:]
assert serial == 'emulator-5580' and package == 'io.ecosystem.ai.preview'


def call(*args):
    return subprocess.check_output([adb, '-s', serial, *args], text=True, timeout=40).strip()


def shell(*args):
    return call('shell', *args)


assert shell('getprop', 'ro.kernel.qemu') == '1'


def ui():
    shell('uiautomator', 'dump', '/data/local/tmp/ecoai-project.xml')
    xml = shell('cat', '/data/local/tmp/ecoai-project.xml')
    Path('android-preview/android-project-ui.xml').write_text(xml)
    root = ET.fromstring(xml)
    texts = [n.get('text', '') for n in root.iter('node')]
    assert 'Operação não concluída' not in texts, texts
    assert 'Ecosystem AI não iniciou' not in texts, texts
    return root


def node(text=None, resource=None):
    root = ui()
    matches = [n for n in root.iter('node') if
               (text is None or n.get('text') == text) and
               (resource is None or n.get('resource-id') == resource)]
    assert len(matches) == 1, (text, resource, ET.tostring(root, encoding='unicode'))
    return matches[0]


def tap(text=None, resource=None):
    target = node(text, resource)
    x1, y1, x2, y2 = map(int, re.findall(r'\d+', target.get('bounds')))
    assert x2 > x1 and y2 > y1
    shell('input', 'tap', str((x1+x2)//2), str((y1+y2)//2))
    time.sleep(.4)


def enter(hint, text):
    tap(hint)
    shell('input', 'text', text)
    shell('input', 'keyevent', '4')  # hide keyboard, keep dialog


def save():
    tap(resource='android:id/button1')


def open_project():
    tap('Abrir projeto')
    node('+ Nova conversa')
    node('CI_project')


tap('+ Novo projeto')
enter('Nome do projeto', 'CI_project')
save()
node('+ Nova conversa')  # calls roster.Read(), beyond the previous home-only smoke
node('0 agentes • 0 equipes')
tap('+ Agente')
enter('Nome', 'CI_agent')
enter('Papel e instruções', 'Offline_reviewer')
save()
node('1 agentes • 0 equipes')
tap('+ Nova conversa')
enter('Título', 'CI_conversation')
save()
node('CI_conversation')
# Kill the process before touching the disposable emulator's private files.
shell('am', 'force-stop', package)
call('root')
call('wait-for-device')
assert shell('id', '-u') == '0', 'Emulator root is required for the OS-alias regression fixture'
data = f'/data/user/0/{package}'
files = data + '/files'
before = json.loads(shell('cat', files + '/catalog/catalog.json'))
roster_before = shell('cat', files + '/catalog/roster.json')
# A real symlink, not a string substitution. Keep the app UID/SELinux labels.
owner = shell('stat', '-c', '%u:%g', files)
context = shell('ls', '-Zd', files).split()[0]
shell('mv', files, data + '/files-canonical')
shell('ln', '-s', data + '/files-canonical', files)
shell('chown', '-h', owner, files)
shell('chcon', '-h', context, files)
shell('ls', '-ldZ', files, data + '/files-canonical')
shell('test', '-L', files)
shell('am', 'start', '-W', '-a', 'android.intent.action.MAIN',
      '-c', 'android.intent.category.LAUNCHER', '-n', package + '/' + activity)
time.sleep(3)
open_project()  # fails on the old AbsolutePath implementation with the reported dialog
node('1 agentes • 0 equipes')
node('CI_conversation')
tap('Abrir conversa')
node('CI_conversation')
assert shell('cat', files + '/catalog/roster.json') == roster_before
updated = json.loads(shell('cat', files + '/catalog/catalog.json'))
# Only the host-owned workspace path and enclosing revision/checksum may change.
old_project = before['body']['projects'][0]
new_project = updated['body']['projects'][0]
assert new_project['workspaceDirectory'] == old_project['workspaceDirectory'].replace('/files/projects/', '/files-canonical/projects/', 1)
new_project['workspaceDirectory'] = old_project['workspaceDirectory']
assert new_project == old_project, 'Project identity, sessions and history must survive alias rebinding'
assert shell('pidof', package)
print('Android 13 project passed: project, agent, conversation and process restart through symlinked private root; data preserved; no provider call.')
