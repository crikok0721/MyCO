import { build } from "esbuild";
import { readFile } from "node:fs/promises";
import { fileURLToPath } from "node:url";
import { dirname, resolve } from "node:path";

const runtimeRoot = dirname(fileURLToPath(import.meta.url));
const versionPropsPath = resolve(runtimeRoot, "../../eng/MyCodex.Version.props");
const versionProps = await readFile(versionPropsPath, "utf8");

function property(name) {
  const match = versionProps.match(new RegExp(`<${name}>([^<]+)</${name}>`));
  if (!match?.[1]) throw new Error(`Missing ${name} in ${versionPropsPath}`);
  return match[1].trim();
}

const runtimeVersion = property("MyCodexVersion");
const protocolVersion = Number(property("MyCodexProtocolVersion"));
const configSchemaVersion = Number(property("MyCodexConfigSchemaVersion"));
const calibrationSchemaVersion = Number(
  property("MyCodexCalibrationSchemaVersion")
);

// Bundle the TypeScript runtime into one self-contained script embedded by the WPF project.
await build({
  entryPoints: ["src/index.ts"],
  outfile: "dist/mycodex.runtime.js",
  bundle: true,
  platform: "browser",
  format: "iife",
  target: ["chrome120"],
  minify: true,
  legalComments: "none",
  sourcemap: false,
  define: {
    __MYCODEX_VERSION__: JSON.stringify(runtimeVersion),
    __MYCODEX_PROTOCOL_VERSION__: JSON.stringify(protocolVersion),
    __MYCODEX_CONFIG_SCHEMA_VERSION__: JSON.stringify(configSchemaVersion),
    __MYCODEX_CALIBRATION_SCHEMA_VERSION__: JSON.stringify(
      calibrationSchemaVersion
    )
  },
  banner: {
    js: `/* MyCodex Skin Runtime v${runtimeVersion} — MIT */`
  }
});
