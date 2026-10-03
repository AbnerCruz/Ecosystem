// UC-2: dados portáveis para identidade e GC, com saídas calculadas pelo domínio atual.
import {readFileSync} from 'node:fs';
import {join} from 'node:path';
import {ROOT} from './v2-docs.mjs';
import {createEnv} from '../../tests/helpers/vault-env.mjs';
export function makeDomainCases(){return JSON.parse(readFileSync(join(ROOT,'docs/csharp/acceptance/domain-cases.json'),'utf8')).cases}
export async function runDomainCase(operation,input){
  const env=createEnv(new Map());const W=env.context.window;
  try {
    if(operation==='identity.text')return {normalized:W.UrbeIdentity.normalize(input.text),fingerprint:W.UrbeIdentity.fingerprint(input.text)};
    if(operation==='identity.parse')return {state:W.UrbeIdentity.parse(input.raw).state};
    if(operation==='identity.pair')return W.UrbeIdentity.pair(input.vanished,input.appeared);
    if(operation==='gc.plan')return W.UrbeGC.plan(input.state,input.options);
    throw new Error('operação de domínio desconhecida');
  } finally {env.p.suspend(true);if(env.p.timer)clearTimeout(env.p.timer)}
}
