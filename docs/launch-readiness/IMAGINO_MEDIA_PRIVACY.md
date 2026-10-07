# Media privacy
P0 remains BLOCKED until historical public access is removed. The new launch backend private API gate passed on 2026-10-07T21:07Z.

## Contract and choice
Choose authenticated Imagino API proxy. New images and videos use the already-private staging bucket `imagino-videos-staging`, deterministic server-owned key `generation-v2/{owner}/{job}.{extension}`, SHA-256, byte length, MIME and OutputStored checkpoint. Views expose `/api/generation/jobs/{id}/media`, never provider/public object URLs. Anonymous 401, foreign 404, owner Completed only; private/no-store, nosniff, no-referrer; authenticated download uses a job-derived filename.
Reference inputs move to `generation-inputs/{owner}/{job}/index.png`; Mongo stores bounded identity/integrity metadata, not new base64 payloads. Existing bounded legacy inputs remain compatible.

Frontend fetches with Authorization, builds scoped blob URLs, aborts/revokes on job change/unmount/logout; Assets images lazy-load. Private media is not stored in localStorage or a service worker. Public image optimization proxy returns 410. JWT never goes in URL.
API File result supports byte Range/206. Present implementation still buffers the full object server-side and the full Blob client-side; it is not streaming. Limits: images 20 MiB, video 100 MiB, MIME plus magic bytes/integrity. Concurrent playback on the intended memory tier must be measured before launch. Short-lived S3 presigned URLs/hybrid could reduce buffering, but bearer URL sharing, expiry, logging and CDN/cache revocation complicate privacy. Do not introduce them without that review.

## Staging and future migration
1. Export job metadata and inventory source object key/hash; preserve originals.
2. Deploy compatible backend/frontend with all real POST flags off.
3. Admin-only bounded per-job `POST /api/generation/operations/jobs/{id}/private-image`: read validated owned historical key through S3, copy to private bucket, validate bytes, CAS Completed metadata, journal migration. Repeat safely. Never trust arbitrary input URL/key.
4. Verify owner bytes/hash, anonymous 401, foreign 404, Range, download, reuse/Animate, and blob revocation.
5. Verify every historical client switched; remove r2.dev, custom-domain and any public Worker access on the old bucket; unauthenticated old URL must fail. Preserve source objects until retention approval.
6. Production migration repeats in batches with separately approved production credentials; no production action in this phase.

Historical app-level URL hiding does not revoke an old object URL. Current staging HEAD 200 proves the unresolved exposure.
Eight Completed images from the existing synthetic owner history were copied with equal before/after SHA-256 and byte counts, preserving original objects and accepted credits. Repeating each migration succeeded without another financial transition. Owner delivery/download works; anonymous 401 and foreign 404; private/no-store and nosniff present. Existing video is 1,616,054 bytes, video/mp4, owner 200 and Range 206/16 bytes. GC dry-run found zero candidates. Wallet and eleven-job history unchanged; no generation or email. Temporary staging operator grant was removed; own operations endpoint returned 403 afterward.
References: [Cloudflare public buckets](https://developers.cloudflare.com/r2/buckets/public-buckets/), [presigned URLs](https://developers.cloudflare.com/r2/api/s3/presigned-urls/).
