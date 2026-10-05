const fs=require('node:fs'); const assert=require('node:assert/strict');const crypto=require('node:crypto');
const base='https://imagino-api-ai-staging.onrender.com';
const credentials=JSON.parse(fs.readFileSync(process.argv[2],'utf8'));const path=process.argv[3];const evidence=JSON.parse(fs.readFileSync(path,'utf8'));
async function main(){
 const login=await fetch(base+'/api/auth/login',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({email:credentials.accounts[0].Email,password:credentials.password})});assert.equal(login.status,200);const {token}=await login.json();
 const headers={Authorization:'Bearer '+token,'Content-Type':'application/json'};
 for(let i=0;i<90;i++){
  const response=await fetch(base+'/api/generation/jobs/'+evidence.id,{headers,signal:AbortSignal.timeout(30000)});assert.equal(response.status,200);const job=await response.json();
  if(job.status==='Completed'){
   assert.equal(job.creditState,'Charged');const balance=await (await fetch(base+'/api/users/credits',{headers})).json();assert.equal(balance.credits,evidence.balanceBefore-1);
   const replay=await fetch(base+'/api/generation/jobs',{method:'POST',headers:{...headers,'Idempotency-Key':evidence.key},body:JSON.stringify(evidence.body)});assert.equal(replay.status,202);assert.equal((await replay.json()).id,evidence.id);
   const after=await (await fetch(base+'/api/users/credits',{headers})).json();assert.equal(after.credits,balance.credits);
   const download=await fetch(base+'/api/generation/jobs/'+job.id+'/download',{headers});assert.equal(download.status,200);const bytes=Buffer.from(await download.arrayBuffer());assert.equal(bytes.length,40046);
   Object.assign(evidence,{outcome:'passed_restart_reclaim',terminal:job,balanceAfter:balance.credits,replayNoDebit:true,download:{status:200,bytes:bytes.length,sha256:crypto.createHash('sha256').update(bytes).digest('hex')},finishedAt:new Date().toISOString()});fs.writeFileSync(path,JSON.stringify(evidence,null,2));console.log(JSON.stringify({outcome:evidence.outcome,id:job.id,balanceBefore:evidence.balanceBefore,balanceAfter:evidence.balanceAfter}));return;
  }
  assert.equal(job.status,'Processing');await new Promise(r=>setTimeout(r,2000));
 }
 throw Error('Reclaimed job did not complete');
}
main().catch(e=>{console.error(e.message);process.exitCode=1;});
