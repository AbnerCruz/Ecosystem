import {runNativeCase} from './lib/parity-native.mjs';
let raw='';for await(const chunk of process.stdin)raw+=chunk;
const request=JSON.parse(raw);
if(request.schemaVersion!==1||!Array.isArray(request.cases))throw new Error('protocolo inválido');
const results=[];
for(const c of request.cases){if(c.operation!=='native.scenario')throw new Error('operação não suportada');results.push({id:c.id,output:await runNativeCase(c.input)})}
// O main instalado cria watchers/timers; a casca simulada não tem app.quit real.
process.stdout.write(JSON.stringify({schemaVersion:1,results}),()=>process.exit(0));
