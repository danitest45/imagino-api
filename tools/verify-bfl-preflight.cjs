// Zero-cost preflight: only quotes and paid-disabled negative requests. Never call BFL directly.
const fs=require('node:fs'),path=require('node:path'),assert=require('node:assert/strict'),crypto=require('node:crypto');
const base='https://imagino-api-ai-staging.onrender.com';
const credentials=JSON.parse(fs.readFileSync(process.argv[2],'utf8'));
const policy=fs.readFileSync(path.join(__dirname,'../Services/Generation/BflHomologationPolicy.cs'),'utf8');
const prompt=name=>JSON.parse('"'+policy.match(new RegExp('const string '+name+' = "([^\\n]+)";'))[1]+'"');
const reference=fs.readFileSync(path.join(__dirname,'../Fixtures/bfl-reference-20261005.png'));
const requests=[{modelId:'flux-fast-20261002',prompt:prompt('FastPrompt')},{modelId:'flux-studio-20261002',prompt:prompt('StudioPrompt')},
 {modelId:'flux-studio-20261002',prompt:prompt('ReferencePrompt'),inputs:[{role:'reference',data:'data:image/png;base64,'+reference.toString('base64')}]}]
 .map(r=>({...r,settings:{resolution:'1MP',aspectRatio:'1:1'},inputs:r.inputs||[]}));
const report={checkedAt:new Date().toISOString(),paidCalls:0,api:base,quotes:[],checks:[]};
async function json(route,method,token,body,key){const r=await fetch(base+route,{method,headers:{'Content-Type':'application/json',...(token?{Authorization:'Bearer '+token}:{}),...(key?{'Idempotency-Key':key}:{})},...(body?{body:JSON.stringify(body)}:{})});return {status:r.status,body:await r.json()};}
async function login(n){const account=credentials.accounts[n],r=await json('/api/auth/login','POST',null,{email:account.Email,password:credentials.password});assert.equal(r.status,200);return r.body.token;}
async function main(){
 assert.equal((await fetch(base+'/health')).status,200);
 const catalog=await json('/api/generation/catalog','GET');assert.equal(catalog.status,200);
 for(const model of catalog.body.models.filter(m=>m.providerModel.startsWith('flux-2-')))assert.equal(model.availability,'approval_required');
 const owner=await login(0),foreign=await login(1);
 const before=await json('/api/users/credits','GET',owner),historyBefore=await json('/api/generation/jobs','GET',owner);
 report.ownerBalanceBefore=before.body.credits;report.historyBefore=historyBefore.body.length;
 for(let i=0;i<requests.length;i++){
  const q=await json('/api/generation/quote','POST',owner,requests[i]);assert.equal(q.status,200);
  assert.equal(q.body.credits,[5,10,15][i]);assert.equal(q.body.providerCostEstimateUsd,[.014,.03,.045][i]);
  report.quotes.push({call:i+1,model:requests[i].modelId,credits:q.body.credits,providerEstimatedUsd:q.body.providerCostEstimateUsd,settings:requests[i].settings,referenceCount:requests[i].inputs.length});
  const denied=await json('/api/generation/jobs','POST',owner,{...requests[i],quoteId:q.body.quoteId},'bfl-disabled-check-20261005-'+(i+1));assert.equal(denied.status,403);
 }
 const foreignQuote=await json('/api/generation/quote','POST',foreign,requests[0]);assert.equal(foreignQuote.status,403);
 report.checks.push({gate:'foreign_quote_denied',status:foreignQuote.status},{gate:'paid_disabled_requests_denied',count:3,status:403});
 const after=await json('/api/users/credits','GET',owner),historyAfter=await json('/api/generation/jobs','GET',owner);
 assert.equal(after.body.credits,before.body.credits);assert.equal(historyAfter.body.length,historyBefore.body.length);
 report.ownerBalanceAfter=after.body.credits;report.historyAfter=historyAfter.body.length;
 report.reference={bytes:reference.length,sha256:crypto.createHash('sha256').update(reference).digest('hex')};report.outcome='passed';
}
main().catch(e=>{report.outcome='failed';report.error=e.message;process.exitCode=1;}).finally(()=>{fs.writeFileSync(process.argv[3],JSON.stringify(report,null,2));console.log(JSON.stringify(report));});
