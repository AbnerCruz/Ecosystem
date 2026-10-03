import {runDomainCase} from './lib/parity-domain.mjs';
let raw='';for await(const c of process.stdin)raw+=c;
const req=JSON.parse(raw);if(req.schemaVersion!==1||!Array.isArray(req.cases))throw new Error('protocolo inválido');
const results=[];for(const c of req.cases)results.push({id:c.id,output:await runDomainCase(c.operation,c.input)});
process.stdout.write(JSON.stringify({schemaVersion:1,results}));
