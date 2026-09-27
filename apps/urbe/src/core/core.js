(function (global) {
  'use strict';

  class EventBus {
    constructor() { this.listeners = new Map(); }
    on(type, listener) {
      if (typeof listener !== 'function') throw new TypeError('listener must be a function');
      const set = this.listeners.get(type) || new Set();
      set.add(listener); this.listeners.set(type, set);
      return () => this.off(type, listener);
    }
    once(type, listener) {
      const off = this.on(type, payload => { off(); listener(payload); });
      return off;
    }
    off(type, listener) {
      const set = this.listeners.get(type);
      if (!set) return false;
      const removed = set.delete(listener);
      if (!set.size) this.listeners.delete(type);
      return removed;
    }
    emit(type, payload) {
      const set = this.listeners.get(type);
      if (!set) return 0;
      [...set].forEach(listener => listener(payload));
      return set.size;
    }
    clear(type) { type ? this.listeners.delete(type) : this.listeners.clear(); }
  }

  class CommandRegistry {
    constructor(events) { this.events = events; this.commands = new Map(); }
    register(id, definition) {
      if (!id || typeof id !== 'string') throw new TypeError('command id is required');
      const def = typeof definition === 'function' ? { execute: definition } : definition;
      if (!def || typeof def.execute !== 'function') throw new TypeError('command execute is required');
      if (this.commands.has(id)) throw new Error('command already registered: ' + id);
      const command = Object.freeze({ id, title: def.title || id, category: def.category || 'General', execute: def.execute, enabled: def.enabled || (() => true) });
      this.commands.set(id, command);
      this.events.emit('command:registered', command);
      return () => this.unregister(id);
    }
    unregister(id) { const command=this.commands.get(id); if(!command) return false; this.commands.delete(id); this.events.emit('command:unregistered', command); return true; }
    has(id) { return this.commands.has(id); }
    get(id) { return this.commands.get(id) || null; }
    list() { return [...this.commands.values()]; }
    execute(id, context) {
      const command = this.get(id);
      if (!command) throw new Error('unknown command: ' + id);
      if (!command.enabled(context)) return false;
      this.events.emit('command:before', { id, context });
      try {
        const result = command.execute(context);
        if (result && typeof result.then === 'function') return result.then(value => { this.events.emit('command:after', { id, context, value }); return value; }, error => { this.events.emit('command:error', { id, context, error }); throw error; });
        this.events.emit('command:after', { id, context, value: result });
        return result;
      } catch (error) { this.events.emit('command:error', { id, context, error }); throw error; }
    }
  }

  class StateStore {
    constructor(events, initialState) { this.events=events; this.state=Object.freeze({ ...(initialState || {}) }); this.version=0; }
    get() { return this.state; }
    select(key) { return this.state[key]; }
    patch(patch, meta) {
      const delta = typeof patch === 'function' ? patch(this.state) : patch;
      if (!delta || typeof delta !== 'object') return this.state;
      const previous=this.state; this.state=Object.freeze({ ...previous, ...delta }); this.version++;
      this.events.emit('state:changed', { previous, current:this.state, patch:delta, version:this.version, meta:meta || null });
      return this.state;
    }
  }

  function createCore() {
    const events = new EventBus();
    const state = new StateStore(events, { workspace:null, activeDocument:null, mode:'workspace', ready:false });
    const commands = new CommandRegistry(events);
    const services = new Map();
    return Object.freeze({
      version: '1.0.0-beta',
      events, state, commands,
      provide(name, service) { if (!name) throw new TypeError('service name is required'); if (services.has(name)) throw new Error('service already provided: '+name); services.set(name, service); events.emit('service:provided',{name,service}); return service; },
      service(name) { return services.get(name) || null; },
      hasService(name) { return services.has(name); },
      start() { if (!state.select('ready')) { state.patch({ready:true},{source:'core.start'}); events.emit('core:ready', this); } return this; }
    });
  }

  global.UrbeCore = global.UrbeCore || createCore();
})(window);
