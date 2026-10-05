// Read/replay only: this final check creates no new job and never dispatches AI.
const fs=require('node:fs');const assert=require('node:assert/strict');const crypto=require('node:crypto');
const base='https://imagino-api-ai-staging.onrender.com',origin='https://imagino-front-git-codex-imagino-ai-776a34-danitest45s-projects.vercel.app';
const credentials=JSON.parse(fs.readFileSync(process.argv[2],'utf8'));
const reclaim=JSON.parse(fs.readFileSync(process.argv[3],'utf8'));
const report={api:base,checkedAt:new Date().toISOString(),checks:[],paidProviderCalls:0};
async function main(){
 const health=await fetch(base+'/health');assert.equal(health.status,200);report.checks.push({gate:'health',status:health.status});
 const catalog=await (await fetch(base+'/api/generation/catalog')).json();assert.equal(catalog.models.length,6);assert.equal(catalog.models.find(m=>m.id==='pipeline-demo-20261002').availability,'synthetic_demo');assert.equal(catalog.models.find(m=>m.id==='flux-fast-20261002').availability,'approval_required');report.catalog=catalog;
 for(const requestOrigin of [origin,'https://foreign.example']){
  const r=await fetch(base+'/api/generation/catalog',{method:'OPTIONS',headers:{Origin:requestOrigin,'Access-Control-Request-Method':'GET'}});const allowed=r.headers.get('access-control-allow-origin');assert.equal(allowed,requestOrigin===origin?origin:null);report.checks.push({gate:'cors',origin:requestOrigin,status:r.status,allowed});
 }
 const balances=[];let ownerToken,foreignToken;
 for(const account of credentials.accounts){
  const r=await fetch(base+'/api/auth/login',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({email:account.Email,password:credentials.password})});assert.equal(r.status,200);const {token}=await r.json();
  const balance=await (await fetch(base+'/api/users/credits',{headers:{Authorization:'Bearer '+token}})).json();balances.push({email:account.Email,credits:balance.credits});if(!ownerToken)ownerToken=token;else if(!foreignToken)foreignToken=token;
 }
 assert.deepEqual(balances.map(x=>x.credits),[16,20,0]);report.balances=balances;
 const headers={Authorization:'Bearer '+ownerToken,'Content-Type':'application/json'};
 const history=await (await fetch(base+'/api/generation/jobs',{headers})).json();assert.equal(history.length,5);assert.equal(history.filter(j=>j.creditState==='Charged').length,4);assert.equal(history.filter(j=>j.creditState==='Refunded').length,1);report.history=history;
 const replay=await fetch(base+'/api/generation/jobs',{method:'POST',headers:{...headers,'Idempotency-Key':reclaim.key},body:JSON.stringify(reclaim.body)});assert.equal(replay.status,202);assert.equal((await replay.json()).id,reclaim.id);
 const after=await (await fetch(base+'/api/users/credits',{headers})).json();assert.equal(after.credits,16);report.checks.push({gate:'replay_after_restart',id:reclaim.id,status:202,balance:16});
 const foreignHistory=await (await fetch(base+'/api/generation/jobs',{headers:{Authorization:'Bearer '+foreignToken}})).json();assert.deepEqual(foreignHistory,[]);
 for(const suffix of ['', '/download']){const response=await fetch(base+'/api/generation/jobs/'+reclaim.id+suffix,{headers:{Authorization:'Bearer '+foreignToken}});assert.equal(response.status,404);report.checks.push({gate:'foreign_owner_hidden',path:suffix,status:404});}
 const object=history.find(j=>j.id===reclaim.id);const stored=await fetch(object.outputUrl);assert.equal(stored.status,200);const bytes=Buffer.from(await stored.arrayBuffer());const sha256=crypto.createHash('sha256').update(bytes).digest('hex');assert.equal(sha256,reclaim.download.sha256);report.r2={objectUrl:object.outputUrl,status:200,bytes:bytes.length,sha256,accessPolicy:'Existing staging images bucket is public; owner authorization applies to API job/history/download endpoints.'};
 report.outcome='passed_final_runtime';
}
main().catch(e=>{report.outcome='failed';report.error=e.message;process.exitCode=1;}).finally(()=>{fs.writeFileSync(process.argv[4],JSON.stringify(report,null,2));console.log(JSON.stringify({outcome:report.outcome,balances:report.balances,checks:report.checks,r2:report.r2,error:report.error}));});
