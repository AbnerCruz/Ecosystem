import {runStorageCase} from './lib/parity-storage.mjs';
let raw='';for await(const c of process.stdin)raw+=c;
const req=JSON.parse(raw);if(req.schemaVersion!==1||!Array.isArray(req.cases))throw new Error('protocolo inválido');
const results=[];for(const c of req.cases){if(c.operation!=='storage.scenario')throw new Error('operação não suportada');results.push({id:c.id,output:await runStorageCase(c.input)})}
process.stdout.write(JSON.stringify({schemaVersion:1,results}));
