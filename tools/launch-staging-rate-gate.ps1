param([Parameter(Mandatory=$true)][string]$WorkspaceRoot)
$ErrorActionPreference='Stop'
$api='https://imagino-api-ai-staging.onrender.com'
$origin='https://imagino-front-git-feat-imagino-laun-348f6b-danitest45s-projects.vercel.app'
$credentials=Get-Content -LiteralPath (Join-Path $WorkspaceRoot 'private/generation-v2-credentials.json') -Raw | ConvertFrom-Json
$owner=$credentials.accounts | Where-Object {$_.Email -eq 'gen-v2-owner-20261002@example.invalid'}
$session=[Microsoft.PowerShell.Commands.WebRequestSession]::new()
$login=$null
try {
  $login=Invoke-RestMethod -Uri ($api+'/api/auth/login') -Method Post -WebSession $session -ContentType 'application/json' -Body (@{email=$owner.Email;password=$credentials.password}|ConvertTo-Json -Compress)
  $headers=@{Authorization='Bearer '+$login.token;Origin=$origin}
  $proof=Invoke-RestMethod -Uri ($api+'/api/generation/runway/e2e-video/proof') -Headers $headers
  if($proof.paidGenerationEnabled -ne $false -or $proof.runwayRealSmokeEnabled -ne $false) {throw 'Paid flags must be off.'}
  $catalog=Invoke-RestMethod -Uri ($api+'/api/generation/catalog') -Headers $headers
  $fixture=$catalog.models | Where-Object {$_.availability -eq 'synthetic_demo' -and $_.mediaType -eq 'image'} | Select-Object -First 1
  if(!$fixture) {throw 'Synthetic quote model required.'}
  $before=Invoke-RestMethod -Uri ($api+'/api/users/credits') -Headers $headers
  $jobs=Invoke-RestMethod -Uri ($api+'/api/generation/jobs') -Headers $headers
  $second=[DateTime]::UtcNow.Second
  if($second -gt 25) {Start-Sleep -Seconds (61-$second)}
  $body=@{modelId=$fixture.id;prompt='Synthetic quote-only limiter gate';settings=@{};inputs=@()} | ConvertTo-Json -Depth 4 -Compress
  $statuses=@();$retryAfter=$null
  for($attempt=0;$attempt -lt 31;$attempt++) {
    $response=Invoke-WebRequest -Uri ($api+'/api/generation/quote') -Method Post -Headers $headers -ContentType 'application/json' -Body $body -SkipHttpErrorCheck
    $statuses+=[int]$response.StatusCode
    if($response.StatusCode -eq 429) {$retryAfter=[string]$response.Headers['Retry-After'];break}
  }
  if($statuses -notcontains 429 -or @($statuses|Where-Object {$_ -notin 200,429}).Count -gt 0 -or !$retryAfter) {throw 'Quote limiter did not prove bounded 429/Retry-After.'}
  $after=Invoke-RestMethod -Uri ($api+'/api/users/credits') -Headers $headers
  $afterJobs=Invoke-RestMethod -Uri ($api+'/api/generation/jobs') -Headers $headers
  if($before.credits -ne $after.credits -or @($jobs).Count -ne @($afterJobs).Count) {throw 'Financial/history state changed.'}
  [ordered]@{atUtc=[DateTime]::UtcNow.ToString('o');serviceId='srv-db1tmv17lnhs73efdjp0';attempts=$statuses.Count;quoteAccepted=@($statuses|Where-Object {$_ -eq 200}).Count;rateLimited=429;retryAfterSeconds=$retryAfter;walletUnchanged=$true;historyUnchanged=$true;newJobs=0;providerPosts=0;emailsSent=0} | ConvertTo-Json
} catch {throw 'Staging quote-only rate gate failed; private error details suppressed.'}
finally {
  if($login) {try {Invoke-RestMethod -Uri ($api+'/api/auth/logout') -Method Post -WebSession $session -Headers @{Origin=$origin}|Out-Null}catch{}}
  $login=$null;$headers=$null;$credentials=$null
}
