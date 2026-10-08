param([Parameter(Mandatory=$true)][string]$WorkspaceRoot)
$ErrorActionPreference='Stop'
$api='https://imagino-api-ai-staging.onrender.com'
$origin='https://imagino-front-git-feat-imagino-laun-348f6b-danitest45s-projects.vercel.app'
$historicalHost='https://pub-56f86851d1884a3b8e7a73f1624e4239.r2.dev'
$ownerId='6ac038cb05509cea703277eb'
$evidencePath=Join-Path $PSScriptRoot '../docs/evidence/launch-readiness/staging-b1-public-off-gate.json'
$baseline=Get-Content -LiteralPath (Join-Path $PSScriptRoot '../docs/evidence/launch-readiness/staging-private-media-gate.json') -Raw | ConvertFrom-Json
$creds=Get-Content -LiteralPath (Join-Path $WorkspaceRoot 'private/generation-v2-credentials.json') -Raw | ConvertFrom-Json
$owner=$creds.accounts | Where-Object {$_.Email -eq 'gen-v2-owner-20261002@example.invalid'}
$session=[Microsoft.PowerShell.Commands.WebRequestSession]::new()
$foreignSession=[Microsoft.PowerShell.Commands.WebRequestSession]::new()
$handler=[System.Net.Http.HttpClientHandler]::new()
$handler.AllowAutoRedirect=$false
$handler.UseCookies=$false
$http=[System.Net.Http.HttpClient]::new($handler)
$http.Timeout=[TimeSpan]::FromSeconds(45)
$login=$null;$foreign=$null;$refreshed=$null;$stage='owner-login'
$started=[DateTime]::UtcNow.ToString('o')

# Fixed staging hosts only. No public request receives an Authorization header,
# cookie, query token or redirect. Error bodies and media bytes never enter evidence.
function HistoricalStatus([string]$id,[string]$method) {
  if($id -notmatch '^[a-f0-9]{24}$') {throw 'Invalid historical fixture identity.'}
  $request=[System.Net.Http.HttpRequestMessage]::new([System.Net.Http.HttpMethod]::new($method),$historicalHost+'/generation-v2/'+$ownerId+'/'+$id+'.png')
  $response=$null
  try {
    $response=$http.SendAsync($request,[System.Net.Http.HttpCompletionOption]::ResponseHeadersRead).GetAwaiter().GetResult()
    return @{status=[int]$response.StatusCode;mime=$response.Content.Headers.ContentType.MediaType;cloudflareResponse=($response.Headers.Contains('CF-Ray') -and [string]$response.Headers.Server -match 'cloudflare')}
  } finally {if($response){$response.Dispose()};$request.Dispose()}
}
function Media([string]$id,[string]$route,[string]$token,[int]$maxBytes,[bool]$range=$false) {
  if($id -notmatch '^[a-f0-9]{24}$' -or $route -notin @('media','download')) {throw 'Invalid owned media route.'}
  $request=[System.Net.Http.HttpRequestMessage]::new([System.Net.Http.HttpMethod]::Get,$api+'/api/generation/jobs/'+$id+'/'+$route)
  if($token){$request.Headers.Authorization=[System.Net.Http.Headers.AuthenticationHeaderValue]::new('Bearer',$token)}
  if($range){$request.Headers.Range=[System.Net.Http.Headers.RangeHeaderValue]::new(0,15)}
  $response=$null;$stream=$null;$buffered=$null
  try {
    $response=$http.SendAsync($request,[System.Net.Http.HttpCompletionOption]::ResponseHeadersRead).GetAwaiter().GetResult()
    $cache=[string]$response.Headers.CacheControl
    $row=[ordered]@{status=[int]$response.StatusCode;bytes=0;mime=$response.Content.Headers.ContentType.MediaType;sha256=$null;first16Sha256=$null;privateNoStore=($cache -match '\bprivate\b' -and $cache -match '\bno-store\b');nosniff=($response.Headers.Contains('X-Content-Type-Options') -and [string]::Join(',', $response.Headers.GetValues('X-Content-Type-Options')) -eq 'nosniff');noReferrer=($response.Headers.Contains('Referrer-Policy') -and [string]::Join(',', $response.Headers.GetValues('Referrer-Policy')) -eq 'no-referrer');attachment=($response.Content.Headers.ContentDisposition.DispositionType -eq 'attachment');contentRange=[string]$response.Content.Headers.ContentRange}
    if($row.status -notin @(200,206)){return $row}
    if($response.Content.Headers.ContentLength -gt $maxBytes){throw 'Media exceeds the fixture bound.'}
    $stream=$response.Content.ReadAsStreamAsync().GetAwaiter().GetResult()
    $buffered=[IO.MemoryStream]::new()
    $chunk=[byte[]]::new(65536)
    while(($count=$stream.Read($chunk,0,$chunk.Length)) -gt 0) {
      if($buffered.Length+$count -gt $maxBytes){throw 'Media exceeds the fixture bound.'}
      $buffered.Write($chunk,0,$count)
    }
    $bytes=$buffered.ToArray()
    $row.bytes=$bytes.Length
    $row.sha256=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes)).ToLowerInvariant()
    if($bytes.Length -ge 16){$prefix=[byte[]]::new(16);[Array]::Copy($bytes,$prefix,16);$row.first16Sha256=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($prefix)).ToLowerInvariant()}
    return $row
  } finally {if($stream){$stream.Dispose()};if($buffered){$buffered.Dispose()};if($response){$response.Dispose()};$request.Dispose()}
}
function AssertPrivate($row,[string]$mime,[int]$bytes,[string]$hash,[bool]$attachment) {
  if($row.status -ne 200 -or $row.bytes -ne $bytes -or $row.mime -ne $mime -or $row.sha256 -ne $hash -or !$row.privateNoStore -or !$row.nosniff -or !$row.noReferrer -or $row.attachment -ne $attachment){throw 'Owned byte/integrity/privacy contract failed.'}
}
function HistorySnapshot($jobs) {
  return (@($jobs | Sort-Object id | Select-Object id,status,mediaType,credits) | ConvertTo-Json -Depth 5 -Compress)
}
try {
  $login=Invoke-RestMethod -Uri ($api+'/api/auth/login') -Method Post -WebSession $session -ContentType 'application/json' -Body (@{email=$owner.Email;password=$creds.password}|ConvertTo-Json -Compress)
  $headers=@{Authorization='Bearer '+$login.token;Origin=$origin}
  $me=Invoke-RestMethod -Uri ($api+'/api/users/me') -Headers $headers
  if($me.id -ne $ownerId){throw 'Synthetic owner identity mismatch.'}
  $stage='disabled-paid-flags-and-readiness'
  $proof=Invoke-RestMethod -Uri ($api+'/api/generation/runway/e2e-video/proof') -Headers $headers
  if($proof.paidGenerationEnabled -ne $false -or $proof.runwayRealSmokeEnabled -ne $false){throw 'Paid flags not proven off.'}
  $ready=Invoke-WebRequest -Uri ($api+'/health/ready')
  $before=Invoke-RestMethod -Uri ($api+'/api/users/credits') -Headers $headers
  $history=Invoke-RestMethod -Uri ($api+'/api/generation/jobs') -Headers $headers
  $images=@($history | Where-Object {$_.status -eq 'Completed' -and $_.mediaType -eq 'image'})
  $video=@($history | Where-Object {$_.status -eq 'Completed' -and $_.mediaType -eq 'video'})
  if($ready.StatusCode -ne 200 -or $images.Count -ne 8 -or $video.Count -ne 1 -or @($baseline.migratedImages).Count -ne 8){throw 'Staging fixture inventory mismatch.'}
  $stage='historical-r2-dev-head-and-get'
  $publicRows=@()
  foreach($fixture in $baseline.migratedImages) {
    if($fixture.jobId -notin $images.id){throw 'Baseline fixture missing from owner history.'}
    $head=HistoricalStatus $fixture.jobId 'HEAD'
    $get=HistoricalStatus $fixture.jobId 'GET'
    $publicRows+=[ordered]@{jobId=$fixture.jobId;head=$head.status;get=$get.status;getMime=$get.mime;cloudflareResponse=($head.cloudflareResponse -and $get.cloudflareResponse)}
    if($head.status -notin @(401,403,404) -or $get.status -notin @(401,403,404) -or !$head.cloudflareResponse -or !$get.cloudflareResponse -or $get.mime -like 'image/*' -or $get.mime -like 'video/*'){throw 'Historical public URL is not proven denied.'}
  }
  $stage='foreign-login'
  $other=$creds.accounts | Where-Object {$_.Email -ne $owner.Email} | Select-Object -First 1
  if(!$other){throw 'Foreign synthetic account fixture missing.'}
  $foreign=Invoke-RestMethod -Uri ($api+'/api/auth/login') -Method Post -WebSession $foreignSession -ContentType 'application/json' -Body (@{email=$other.Email;password=$creds.password}|ConvertTo-Json -Compress)
  $imageRows=@()
  foreach($fixture in $baseline.migratedImages) {
    $stage='owner-image-and-download-integrity'
    $job=$images | Where-Object {$_.id -eq $fixture.jobId}
    if($job.outputUrl -ne ('/api/generation/jobs/'+$fixture.jobId+'/media')){throw 'History must expose the authenticated media route.'}
    $media=Media $fixture.jobId 'media' $login.token $fixture.bytes
    $download=Media $fixture.jobId 'download' $login.token $fixture.bytes
    AssertPrivate $media 'image/png' $fixture.bytes $fixture.sha256 $false
    AssertPrivate $download 'image/png' $fixture.bytes $fixture.sha256 $true
    $stage='image-ownership-negatives'
    $anonymousMedia=Media $fixture.jobId 'media' '' $fixture.bytes
    $anonymousDownload=Media $fixture.jobId 'download' '' $fixture.bytes
    $foreignMedia=Media $fixture.jobId 'media' $foreign.token $fixture.bytes
    $foreignDownload=Media $fixture.jobId 'download' $foreign.token $fixture.bytes
    if($anonymousMedia.status -ne 401 -or $anonymousDownload.status -ne 401 -or $foreignMedia.status -ne 404 -or $foreignDownload.status -ne 404){throw 'Image ownership negative failed.'}
    $imageRows+=[ordered]@{jobId=$fixture.jobId;bytes=$media.bytes;sha256=$media.sha256;mime=$media.mime;media=$media.status;download=$download.status;matchesPreDisableCopy=$true;privateNoStore=$true;nosniff=$true;noReferrer=$true;downloadAttachment=$true;anonymousMedia=$anonymousMedia.status;anonymousDownload=$anonymousDownload.status;foreignMedia=$foreignMedia.status;foreignDownload=$foreignDownload.status}
  }
  $stage='owner-video-download-and-range'
  $videoJob=$video[0]
  $videoMedia=Media $videoJob.id 'media' $login.token 1616054
  $videoDownload=Media $videoJob.id 'download' $login.token 1616054
  AssertPrivate $videoMedia 'video/mp4' 1616054 $videoMedia.sha256 $false
  AssertPrivate $videoDownload 'video/mp4' 1616054 $videoMedia.sha256 $true
  $videoRange=Media $videoJob.id 'media' $login.token 16 $true
  $downloadRange=Media $videoJob.id 'download' $login.token 16 $true
  foreach($range in @($videoRange,$downloadRange)) {
    if($range.status -ne 206 -or $range.bytes -ne 16 -or $range.sha256 -ne $videoMedia.first16Sha256 -or $range.contentRange -ne 'bytes 0-15/1616054' -or !$range.privateNoStore -or !$range.nosniff -or !$range.noReferrer){throw 'Private video Range contract failed.'}
  }
  $stage='video-ownership-negatives'
  $anonVideoMedia=Media $videoJob.id 'media' '' 1616054
  $anonVideoDownload=Media $videoJob.id 'download' '' 1616054
  $foreignVideoMedia=Media $videoJob.id 'media' $foreign.token 1616054
  $foreignVideoDownload=Media $videoJob.id 'download' $foreign.token 1616054
  if($anonVideoMedia.status -ne 401 -or $anonVideoDownload.status -ne 401 -or $foreignVideoMedia.status -ne 404 -or $foreignVideoDownload.status -ne 404){throw 'Video ownership negative failed.'}
  $stage='operator-role-remains-removed'
  $operations=Invoke-WebRequest -Uri ($api+'/api/generation/operations/stuck') -Headers $headers -SkipHttpErrorCheck
  if($operations.StatusCode -ne 403){throw 'Temporary operator role unexpectedly available.'}
  $stage='refresh-and-financial-history-invariants'
  $refreshed=Invoke-RestMethod -Uri ($api+'/api/auth/refresh') -Method Post -WebSession $session -Headers @{Origin=$origin}
  if(!$refreshed.token){throw 'Owner refresh failed.'}
  $afterHeaders=@{Authorization='Bearer '+$refreshed.token}
  $after=Invoke-RestMethod -Uri ($api+'/api/users/credits') -Headers $afterHeaders
  $afterHistory=Invoke-RestMethod -Uri ($api+'/api/generation/jobs') -Headers $afterHeaders
  if($before.credits -ne $after.credits -or (HistorySnapshot $history) -ne (HistorySnapshot $afterHistory)){throw 'Wallet/history/accepted credits changed.'}
  $stage='logout'
  Invoke-RestMethod -Uri ($api+'/api/auth/logout') -Method Post -WebSession $session -Headers @{Origin=$origin}|Out-Null
  Invoke-RestMethod -Uri ($api+'/api/auth/logout') -Method Post -WebSession $foreignSession -Headers @{Origin=$origin}|Out-Null
  $login=$null;$foreign=$null
  $result=[ordered]@{gate='B1';status='PASS';startedAtUtc=$started;completedAtUtc=[DateTime]::UtcNow.ToString('o');serviceId='srv-db1tmv17lnhs73efdjp0';scope='Existing synthetic owner assets and historical imagino-images-staging r2.dev URLs; production untouched';historicalPublicUrl=@{host=$historicalHost;redirectsFollowed=$false;authorizationSent=$false;cookiesSent=$false;fixtures=$publicRows};images=$imageRows;video=@{jobId=$videoJob.id;media=$videoMedia.status;download=$videoDownload.status;mime=$videoMedia.mime;bytes=$videoMedia.bytes;sha256=$videoMedia.sha256;downloadMatchesMedia=$true;mediaRange=$videoRange.status;downloadRange=$downloadRange.status;rangeBytes=$videoRange.bytes;contentRange=$videoRange.contentRange;rangeMatchesFullPrefix=$true;privateNoStore=$true;nosniff=$true;noReferrer=$true;downloadAttachment=$true;anonymousMedia=$anonVideoMedia.status;anonymousDownload=$anonVideoDownload.status;foreignMedia=$foreignVideoMedia.status;foreignDownload=$foreignVideoDownload.status};readiness=[int]$ready.StatusCode;paidGenerationEnabled=$proof.paidGenerationEnabled;runwayRealSmokeEnabled=$proof.runwayRealSmokeEnabled;ownOperations=[int]$operations.StatusCode;refresh=$true;logout=$true;walletUnchanged=$true;historyUnchanged=$true;acceptedCreditsUnchanged=$true;historyCount=@($history).Count;requestsInitiatedByGate=@{jobCreates=0;providerPosts=0;emailsSent=0;cloudConfigurationWrites=0;objectDeletes=0};limitations=@('Custom-domain/Worker route inventory was not independently verified; no Cloudflare management connector is available.','Video hash proves current media/download equality; the earlier video evidence recorded length, MIME and Range, not a pre-disable hash.','HTTP media/download and Range gate only; browser playback was verified separately before public access removal.')}
  $result | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $evidencePath -Encoding utf8NoBOM
  [ordered]@{gate='B1';status='PASS';historicalHeadStatuses=@($publicRows | ForEach-Object {$_['head']} | Sort-Object -Unique);historicalGetStatuses=@($publicRows | ForEach-Object {$_['get']} | Sort-Object -Unique);images=$imageRows.Count;videoBytes=$videoMedia.bytes;videoRange=$videoRange.status;walletUnchanged=$true;historyCount=@($history).Count;providerPosts=0;jobCreates=0;emailsSent=0} | ConvertTo-Json -Depth 4
} catch {
  $safeStatus=if($_.Exception.Response){[int]$_.Exception.Response.StatusCode}else{$null}
  throw ('B1 staging gate failed at '+$stage+'; HTTP='+$safeStatus+'; type='+$_.Exception.GetType().Name+'; scriptLine='+$_.InvocationInfo.ScriptLineNumber+'. Credentials and response bodies withheld.')
} finally {
  if($login){try{Invoke-RestMethod -Uri ($api+'/api/auth/logout') -Method Post -WebSession $session -Headers @{Origin=$origin}|Out-Null}catch{}}
  if($foreign){try{Invoke-RestMethod -Uri ($api+'/api/auth/logout') -Method Post -WebSession $foreignSession -Headers @{Origin=$origin}|Out-Null}catch{}}
  $login=$null;$foreign=$null;$refreshed=$null;$creds=$null;$headers=$null;$afterHeaders=$null;$http.Dispose()
}
