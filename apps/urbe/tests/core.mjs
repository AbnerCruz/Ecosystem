import fs from 'node:fs';
import vm from 'node:vm';

const source = fs.readFileSync(new URL('../src/core/core.js', import.meta.url), 'utf8');
const context = { window: {} };
vm.createContext(context);
vm.runInContext(source, context);

const core = context.window.UrbeCore;
if (!core) throw new Error('UrbeCore was not created');

let observed = null;
const off = core.events.on('test:event', value => { observed = value; });
core.events.emit('test:event', 42);
if (observed !== 42) throw new Error('EventBus did not deliver payload');
off();
observed = null;
core.events.emit('test:event', 7);
if (observed !== null) throw new Error('EventBus unsubscribe failed');

core.commands.register('test.echo', { title:'Echo', execute: value => value });
if (core.commands.execute('test.echo', 'ok') !== 'ok') throw new Error('CommandRegistry execute failed');

let change = null;
core.events.once('state:changed', value => { change = value; });
core.state.patch({ activeDocument:'note.md' }, { source:'test' });
if (core.state.select('activeDocument') !== 'note.md' || !change) throw new Error('StateStore patch failed');

const service = { ready:true };
core.provide('test.service', service);
if (core.service('test.service') !== service) throw new Error('Service registry failed');

core.start();
if (!core.state.select('ready')) throw new Error('Core did not start');

console.log('OK   EventBus');
console.log('OK   CommandRegistry');
console.log('OK   StateStore');
console.log('OK   Service registry');
console.log('OK   Core lifecycle');
