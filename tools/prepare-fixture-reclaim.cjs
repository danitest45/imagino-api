// Only creates a zero-cost fixture job for the operator's restart/reclaim gate.
const fs = require('node:fs');
const crypto = require('node:crypto');
const assert = require('node:assert/strict');
const base = 'https://imagino-api-ai-staging.onrender.com';
const credentials = JSON.parse(fs.readFileSync(process.argv[2], 'utf8'));
async function main() {
  const login = await fetch(base + '/api/auth/login', {method:'POST', headers:{'Content-Type':'application/json'},body:JSON.stringify({email:credentials.accounts[0].Email,password:credentials.password})});
  assert.equal(login.status,200); const {token} = await login.json();
  const headers = {Authorization:'Bearer '+token,'Content-Type':'application/json'};
  const catalog = await (await fetch(base + '/api/generation/catalog')).json();
  assert.equal(catalog.models.find(m=>m.id==='pipeline-demo-20261002').availability,'synthetic_demo');
  assert.equal(catalog.models.find(m=>m.id==='flux-fast-20261002').availability,'approval_required');
  const before = await (await fetch(base+'/api/users/credits',{headers})).json();
  const body = {modelId:'pipeline-demo-20261002',prompt:'Synthetic restart reclaim acceptance 2026-10-05',settings:{resolution:'1MP',outcome:'success'},inputs:[]};
  const quote = await (await fetch(base+'/api/generation/quote',{method:'POST',headers,body:JSON.stringify(body)})).json();assert.equal(quote.credits,1);
  const key=crypto.randomUUID(); body.quoteId=quote.quoteId;
  const created = await fetch(base+'/api/generation/jobs',{method:'POST',headers:{...headers,'Idempotency-Key':key},body:JSON.stringify(body)});
  assert.equal(created.status,202); const job=await created.json();assert.equal(job.creditState,'Reserved');
  for(let i=0;i<35;i++) {
    const current=await (await fetch(base+'/api/generation/jobs/'+job.id,{headers})).json();
    if(current.status==='Processing') {
      const evidence={id:job.id,owner:credentials.accounts[0].Id ?? '6ac038cb05509cea703277eb',key,body,balanceBefore:before.credits,reservation:job,processing:current,preparedAt:new Date().toISOString()};
      fs.writeFileSync(process.argv[3],JSON.stringify(evidence,null,2));console.log(JSON.stringify({id:job.id,status:current.status,balanceBefore:before.credits}));return;
    }
    assert(!['Completed','Failed','Cancelled'].includes(current.status),'Fixture finished before hold');
    await new Promise(r=>setTimeout(r,100));
  }
  throw Error('Processing fixture was not observed');
}
main().catch(e=>{console.error(e.message);process.exitCode=1;});
