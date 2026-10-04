import {runUpdateCase} from './lib/parity-update.mjs';
let raw='';for await(const c of process.stdin)raw+=c;
const req=JSON.parse(raw);if(req.schemaVersion!==1||!Array.isArray(req.cases))throw new Error('protocolo inválido');
const results=[];for(const c of req.cases){if(c.operation!=='update.scenario')throw new Error('operação não suportada');results.push({id:c.id,output:await runUpdateCase(c.input)})}
process.stdout.write(JSON.stringify({schemaVersion:1,results}));
