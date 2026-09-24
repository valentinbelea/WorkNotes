// Rebuild: npm ci && npm run build (from Solution/tools/codemirror). Writes the bundle and the license notices.
import { build } from "esbuild";
import { readFileSync, writeFileSync, readdirSync, existsSync } from "node:fs";
import { join } from "node:path";

const out = new URL("../../WorkNotes.Web/wwwroot/lib/codemirror/", import.meta.url);
await build({
    entryPoints: ["entry.js"],
    bundle: true,
    format: "esm",
    minify: true,
    legalComments: "none",
    target: "es2020",
    outfile: new URL("codemirror.js", out).pathname
});

// Every bundled package is MIT licensed; keep their copyright and license texts next to the bundle.
const packages = JSON.parse(readFileSync("package-lock.json", "utf8")).packages;
const notices = Object.entries(packages)
    .filter(([path, info]) => path.startsWith("node_modules/") && !info.dev)
    .map(([path, info]) => {
        const dir = path;
        const licenseFile = readdirSync(dir).find(name => /^licen[cs]e/i.test(name));
        const text = licenseFile ? readFileSync(join(dir, licenseFile), "utf8").trim() : `License: ${info.license}`;
        return `${path.slice("node_modules/".length)} ${info.version} (${info.license})\n\n${text}`;
    });
writeFileSync(new URL("THIRD-PARTY-NOTICES.txt", out),
    "WorkNotes bundles the following packages in codemirror.js.\n\n" + notices.join("\n\n" + "-".repeat(72) + "\n\n") + "\n");
console.log(`bundled ${notices.length} packages`);
