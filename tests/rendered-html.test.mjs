import assert from "node:assert/strict";
import { access, readFile } from "node:fs/promises";
import test from "node:test";

async function render() {
  const workerUrl = new URL("../dist/server/index.js", import.meta.url);
  workerUrl.searchParams.set("test", `${process.pid}-${Date.now()}`);
  const { default: worker } = await import(workerUrl.href);

  return worker.fetch(
    new Request("http://localhost/", { headers: { accept: "text/html" } }),
    { ASSETS: { fetch: async () => new Response("Not found", { status: 404 }) } },
    { waitUntil() {}, passThroughOnException() {} },
  );
}

test("server-renders the ResoDrive landing page", async () => {
  const response = await render();
  assert.equal(response.status, 200);
  assert.match(response.headers.get("content-type") ?? "", /^text\/html\b/i);

  const html = await response.text();
  assert.match(html, /<title>ResoDrive: Free Nextcloud, WebDAV and SFTP Drives for Windows<\/title>/i);
  assert.match(html, /Your cloud files, at home in Windows/);
  assert.match(html, /Completely free and open source/);
  assert.match(html, /SoftwareApplication/);
  assert.match(html, /rel="canonical" href="https:\/\/alphasixtyfive\.github\.io\/resodrive\/"/);
  assert.doesNotMatch(html, /src:\s*url\(D:|\.vinext\/fonts|manrope-/i);
  assert.match(html, /Copy and sync jobs/);
  assert.match(html, /For Windows teams/);
  assert.match(html, /Profile examples/);
  assert.match(html, /releases\/latest\/download\/ResoDrive-Setup\.exe/);
  assert.doesNotMatch(html, /codex-preview|Your site is taking shape/i);
});

test("removes starter-only preview code and dependencies", async () => {
  const [page, layout, packageJson] = await Promise.all([
    readFile(new URL("../app/page.tsx", import.meta.url), "utf8"),
    readFile(new URL("../app/layout.tsx", import.meta.url), "utf8"),
    readFile(new URL("../package.json", import.meta.url), "utf8"),
  ]);

  assert.match(page, /company-nextcloud/);
  assert.match(page, /team-webdav/);
  assert.match(page, /project-sftp/);
  assert.match(layout, /ResoDrive: Free Nextcloud, WebDAV and SFTP/);
  assert.doesNotMatch(packageJson, /react-loading-skeleton/);
  await assert.rejects(access(new URL("../app/_sites-preview/SkeletonPreview.tsx", import.meta.url)));
  await assert.rejects(access(new URL("../app/_sites-preview/preview.css", import.meta.url)));
});
