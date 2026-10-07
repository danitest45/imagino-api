param([Parameter(Mandatory=$true)][string]$WorkspaceRoot)
$ErrorActionPreference='Stop'
$api='https://imagino-api-ai-staging.onrender.com'
$origin='https://imagino-front-git-feat-imagino-laun-348f6b-danitest45s-projects.vercel.app'
$creds=Get-Content -LiteralPath (Join-Path $WorkspaceRoot 'private/generation-v2-credentials.json') -Raw | ConvertFrom-Json
$account=$creds.accounts | Where-Object {$_.Email -eq 'gen-v2-owner-20261002@example.invalid'}
$session=[Microsoft.PowerShell.Commands.WebRequestSession]::new()
$login=$null;$foreign=$null
$stage='login'
$http=[System.Net.Http.HttpClient]::new()
function Media([string]$id,[string]$token,[bool]$range=$false) {
  $request=[System.Net.Http.HttpRequestMessage]::new([System.Net.Http.HttpMethod]::Get,$api+'/api/generation/jobs/'+$id+'/media')
  if($token) {$request.Headers.Authorization=[System.Net.Http.Headers.AuthenticationHeaderValue]::new('Bearer',$token)}
  if($range) {$request.Headers.Range=[System.Net.Http.Headers.RangeHeaderValue]::new(0,15)}
  $response=$http.SendAsync($request).GetAwaiter().GetResult()
  try {
    $bytes=$response.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult()
    $cache=[string]$response.Headers.CacheControl
    return @{status=[int]$response.StatusCode;bytes=$bytes.Length;sha256=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes)).ToLowerInvariant();mime=$response.Content.Headers.ContentType.MediaType;privateNoStore=($cache -match '\bprivate\b' -and $cache -match '\bno-store\b');nosniff=($response.Headers.Contains('X-Content-Type-Options'))}
  } finally {$response.Dispose();$request.Dispose()}
}
try {
  $login=Invoke-RestMethod -Uri ($api+'/api/auth/login') -Method Post -WebSession $session -ContentType 'application/json' -Body (@{email=$account.Email;password=$creds.password}|ConvertTo-Json -Compress)
  $headers=@{Authorization='Bearer '+$login.token;Origin=$origin}
  $me=Invoke-RestMethod -Uri ($api+'/api/users/me') -Headers $headers
  if($me.id -ne '6ac038cb05509cea703277eb') {throw 'Synthetic owner binding mismatch.'}
  $proof=Invoke-RestMethod -Uri ($api+'/api/generation/runway/e2e-video/proof') -Headers $headers
  if($proof.paidGenerationEnabled -ne $false -or $proof.runwayRealSmokeEnabled -ne $false) {throw 'Paid flags are not proven off.'}
  $ready=Invoke-WebRequest -Uri ($api+'/health/ready')
  $stage='operator-role-and-emergency-stop'
  $operations=Invoke-RestMethod -Uri ($api+'/api/generation/operations/stuck') -Headers $headers
  if($operations.emergencyStop -ne $true) {throw 'Emergency stop is not proven on.'}
  $before=Invoke-RestMethod -Uri ($api+'/api/users/credits') -Headers $headers
  $history=Invoke-RestMethod -Uri ($api+'/api/generation/jobs') -Headers $headers
  $images=@($history | Where-Object {$_.status -eq 'Completed' -and $_.mediaType -eq 'image'})
  if($images.Count -lt 1 -or $images.Count -gt 30) {throw 'Migration must remain bounded to this synthetic owner history.'}
  $rows=@()
  foreach($job in $images) {
    $stage='owned-image-private-copy-and-hash'
    $original=Media $job.id $login.token
    if($original.status -ne 200) {throw 'Owned media unavailable before copy.'}
    $path=$api+'/api/generation/operations/jobs/'+$job.id+'/private-image'
    $copied=Invoke-RestMethod -Uri $path -Method Post -Headers $headers
    $again=Invoke-RestMethod -Uri $path -Method Post -Headers $headers
    $private=Media $job.id $login.token
    $view=Invoke-RestMethod -Uri ($api+'/api/generation/jobs/'+$job.id) -Headers $headers
    if(!$copied.migrated -or !$again.migrated -or $original.sha256 -ne $private.sha256 -or !$private.privateNoStore -or !$private.nosniff -or $view.outputUrl -ne ('/api/generation/jobs/'+$job.id+'/media') -or $view.credits -ne $job.credits) {throw 'Private migration invariant failed.'}
    $rows+=@{jobId=$job.id;bytes=$private.bytes;sha256=$private.sha256;mime=$private.mime;repeatMigration=$true;acceptedCreditsUnchanged=$true}
  }
  $video=$history | Where-Object {$_.status -eq 'Completed' -and $_.mediaType -eq 'video'} | Select-Object -First 1
  $stage='video-range'
  $videoMedia=Media $video.id $login.token
  $videoRange=Media $video.id $login.token $true
  if($videoMedia.status -ne 200 -or $videoMedia.mime -ne 'video/mp4' -or $videoRange.status -ne 206 -or $videoRange.bytes -ne 16) {throw 'Private video/Range invariant failed.'}
  $anonymous=Media $images[0].id ''
  $stage='ownership-negatives'
  $other=$creds.accounts | Where-Object {$_.Email -ne $account.Email} | Select-Object -First 1
  $foreignSession=[Microsoft.PowerShell.Commands.WebRequestSession]::new()
  $foreign=Invoke-RestMethod -Uri ($api+'/api/auth/login') -Method Post -WebSession $foreignSession -ContentType 'application/json' -Body (@{email=$other.Email;password=$creds.password}|ConvertTo-Json -Compress)
  $foreignMedia=Media $images[0].id $foreign.token
  $foreignOperations=Invoke-WebRequest -Uri ($api+'/api/generation/operations/stuck') -Headers @{Authorization='Bearer '+$foreign.token} -SkipHttpErrorCheck
  if($anonymous.status -ne 401 -or $foreignMedia.status -ne 404 -or $foreignOperations.StatusCode -ne 403) {throw 'Ownership/operator negative failed.'}
  $gc=Invoke-RestMethod -Uri ($api+'/api/generation/operations/media-gc') -Headers $headers
  $stage='gc-and-completed-reconcile'
  if(!$gc.dryRun -or $gc.deletionSupported) {throw 'GC must remain read-only.'}
  $reconcile=Invoke-WebRequest -Uri ($api+'/api/generation/operations/jobs/'+$images[0].id+'/reconcile') -Method Post -Headers $headers -SkipHttpErrorCheck
  if($reconcile.StatusCode -ne 404) {throw 'Completed reconciliation must not mutate or resubmit.'}
  $catalog=Invoke-RestMethod -Uri ($api+'/api/generation/catalog') -Headers $headers
  $stage='quote-and-disabled-provider'
  $fixture=$catalog.models | Where-Object {$_.availability -eq 'synthetic_demo' -and $_.mediaType -eq 'image'} | Select-Object -First 1
  $quoteBody=@{modelId=$fixture.id;prompt='Synthetic quote readiness test';settings=@{};inputs=@()}|ConvertTo-Json -Depth 4 -Compress
  $quote=Invoke-RestMethod -Uri ($api+'/api/generation/quote') -Method Post -Headers $headers -ContentType 'application/json' -Body $quoteBody
  $disabled=$catalog.models | Where-Object {$_.availability -eq 'disabled' -and $_.mediaType -eq 'image' -and $_.capabilities -contains 'textToImage'} | Select-Object -First 1
  $blockedBody=@{modelId=$disabled.id;prompt='Synthetic disabled-provider gate';settings=@{};inputs=@();quoteId=$quote.quoteId}|ConvertTo-Json -Depth 4 -Compress
  $blocked=Invoke-WebRequest -Uri ($api+'/api/generation/jobs') -Method Post -Headers ($headers+@{'Idempotency-Key'='launch-negative-disabled-20261007'}) -ContentType 'application/json' -Body $blockedBody -SkipHttpErrorCheck
  if($blocked.StatusCode -ne 403) {throw 'Disabled provider gate did not reject creation.'}
  $after=Invoke-RestMethod -Uri ($api+'/api/users/credits') -Headers $headers
  $stage='financial-history-and-old-public-url'
  $afterHistory=Invoke-RestMethod -Uri ($api+'/api/generation/jobs') -Headers $headers
  if($after.credits -ne $before.credits -or @($afterHistory).Count -ne @($history).Count) {throw 'Financial/history invariants changed.'}
  $publicHead=Invoke-WebRequest -Uri ('https://pub-56f86851d1884a3b8e7a73f1624e4239.r2.dev/generation-v2/'+$me.id+'/'+$images[0].id+'.png') -Method Head -SkipHttpErrorCheck
  [ordered]@{atUtc=[DateTime]::UtcNow.ToString('o');serviceId='srv-db1tmv17lnhs73efdjp0';readiness=[int]$ready.StatusCode;paidGenerationEnabled=$false;emergencyStop=$true;migratedImages=$rows;video=@{status=$videoMedia.status;bytes=$videoMedia.bytes;mime=$videoMedia.mime;range=$videoRange.status;rangeBytes=$videoRange.bytes};anonymousImage=$anonymous.status;foreignImage=$foreignMedia.status;foreignOperations=[int]$foreignOperations.StatusCode;gc=@{dryRun=$true;deletionSupported=$false;candidates=@($gc.candidates).Count;truncated=$gc.truncated};completedReconcile=[int]$reconcile.StatusCode;syntheticQuoteCredits=$quote.credits;disabledProviderCreation=[int]$blocked.StatusCode;walletUnchanged=$true;historyUnchanged=$true;historicalPublicHead=[int]$publicHead.StatusCode;providerPosts=0;newJobs=0;emailsSent=0} | ConvertTo-Json -Depth 7
} catch {
  $safeStatus=if($_.Exception.Response) {[int]$_.Exception.Response.StatusCode} else {$null}
  throw ('Private staging gate failed at '+$stage+'; HTTP='+$safeStatus+'; type='+$_.Exception.GetType().Name+'; scriptLine='+$_.InvocationInfo.ScriptLineNumber+'. Original objects retained.')
}
finally {
  if($login) {try {Invoke-RestMethod -Uri ($api+'/api/auth/logout') -Method Post -WebSession $session -Headers @{Origin=$origin}|Out-Null}catch{}}
  if($foreign) {try {Invoke-RestMethod -Uri ($api+'/api/auth/logout') -Method Post -WebSession $foreignSession -Headers @{Origin=$origin}|Out-Null}catch{}}
  $login=$null;$foreign=$null;$creds=$null;$headers=$null;$http.Dispose()
}
