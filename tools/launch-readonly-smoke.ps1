param([Parameter(Mandatory=$true)][string]$WorkspaceRoot)
$ErrorActionPreference='Stop'
$api='https://imagino-api-ai-staging.onrender.com'
$origin='https://imagino-front-git-feat-imagino-crea-9cdb36-danitest45s-projects.vercel.app'
$creds=Get-Content -LiteralPath (Join-Path $WorkspaceRoot 'private/generation-v2-credentials.json') -Raw | ConvertFrom-Json
$owner=$creds.accounts | Where-Object {$_.Email -eq 'gen-v2-owner-20261002@example.invalid'}
$session=[Microsoft.PowerShell.Commands.WebRequestSession]::new()
$login=$null
function Status([string]$path,[hashtable]$headers=@{}) {
  try { return [int](Invoke-WebRequest -Uri ($api+$path) -Headers $headers).StatusCode }
  catch { if($_.Exception.Response) { return [int]$_.Exception.Response.StatusCode }; throw 'Read-only smoke transport failed.' }
}
try {
  $login=Invoke-RestMethod -Uri ($api+'/api/auth/login') -Method Post -WebSession $session -ContentType 'application/json' -Body (@{email=$owner.Email;password=$creds.password}|ConvertTo-Json -Compress)
  $headers=@{Authorization='Bearer '+$login.token}
  $before=Invoke-RestMethod -Uri ($api+'/api/users/credits') -Headers $headers
  $history=Invoke-RestMethod -Uri ($api+'/api/generation/jobs') -Headers $headers
  $catalog=Invoke-RestMethod -Uri ($api+'/api/generation/catalog') -Headers $headers
  $proof=Invoke-RestMethod -Uri ($api+'/api/generation/runway/e2e-video/proof') -Headers $headers
  if($proof.paidGenerationEnabled -ne $false) {throw 'Paid submission flag is not proven disabled.'}
  $image=$history | Where-Object {$_.status -eq 'Completed' -and $_.mediaType -eq 'image'} | Select-Object -First 1
  $video=$history | Where-Object {$_.status -eq 'Completed' -and $_.mediaType -eq 'video'} | Select-Object -First 1
  $checks=@{health=(Status '/health');imageDownload=(Status ('/api/generation/jobs/'+$image.id+'/download') $headers);videoDownload=(Status ('/api/generation/jobs/'+$video.id+'/download') $headers);anonymousImage=(Status ('/api/generation/jobs/'+$image.id+'/download'))}
  $checks.paidGenerationEnabled=$proof.paidGenerationEnabled
  $checks.realSmokeEnabled=$proof.runwayRealSmokeEnabled
  $checks.newMediaEndpoint=Status ('/api/generation/jobs/'+$image.id+'/media') $headers
  $other=$creds.accounts | Where-Object {$_.Email -ne $owner.Email} | Select-Object -First 1
  if($other) {
    $foreignSession=[Microsoft.PowerShell.Commands.WebRequestSession]::new()
    $foreign=Invoke-RestMethod -Uri ($api+'/api/auth/login') -Method Post -WebSession $foreignSession -ContentType 'application/json' -Body (@{email=$other.Email;password=$creds.password}|ConvertTo-Json -Compress)
    $checks.foreignImage=Status ('/api/generation/jobs/'+$image.id+'/download') @{Authorization='Bearer '+$foreign.token}
    Invoke-RestMethod -Uri ($api+'/api/auth/logout') -Method Post -WebSession $foreignSession -Headers @{Origin=$origin} | Out-Null
    $foreign=$null
  }
  $refreshed=Invoke-RestMethod -Uri ($api+'/api/auth/refresh') -Method Post -WebSession $session -Headers @{Origin=$origin}
  $checks.refresh=([bool]$refreshed.token)
  $after=Invoke-RestMethod -Uri ($api+'/api/users/credits') -Headers @{Authorization='Bearer '+$refreshed.token}
  $checks.walletUnchanged=($before.credits -eq $after.credits)
  $checks.historicalImageStillPublicUrl=($image.outputUrl -like 'https://pub-*.r2.dev/*')
  $checks.historicalPublicHead=$null
  if($checks.historicalImageStillPublicUrl) { try { $checks.historicalPublicHead=[int](Invoke-WebRequest -Uri $image.outputUrl -Method Head).StatusCode } catch { if($_.Exception.Response) {$checks.historicalPublicHead=[int]$_.Exception.Response.StatusCode} } }
  Invoke-RestMethod -Uri ($api+'/api/auth/logout') -Method Post -WebSession $session -Headers @{Origin=$origin} | Out-Null
  $checks.logout=$true
  [ordered]@{atUtc=[DateTime]::UtcNow.ToString('o');serviceId='srv-db1tmv17lnhs73efdjp0';checks=$checks;historyCount=@($history).Length;providerPosts=0;jobCreates=0;emailsSent=0;newMediaEndpointAvailable=($checks.newMediaEndpoint -eq 200)} | ConvertTo-Json -Depth 6
} catch { throw 'Read-only staging smoke failed; inspect status with credentials kept private.' }
finally {
  if($login) { try {Invoke-RestMethod -Uri ($api+'/api/auth/logout') -Method Post -WebSession $session -Headers @{Origin=$origin}|Out-Null} catch {} }
  $login=$null;$refreshed=$null;$headers=$null;$creds=$null
}
