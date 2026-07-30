import { build } from "esbuild";
import { readFile } from "node:fs/promises";
import { fileURLToPath } from "node:url";
import { dirname, resolve } from "node:path";

const runtimeRoot = dirname(fileURLToPath(import.meta.url));
const versionPropsPath = resolve(runtimeRoot, "../../eng/MyCO.Version.props");
const versionProps = await readFile(versionPropsPath, "utf8");

function property(name) {
  const match = versionProps.match(new RegExp(`<${name}>([^<]+)</${name}>`));
  if (!match?.[1]) throw new Error(`Missing ${name} in ${versionPropsPath}`);
  return match[1].trim();
}

const runtimeVersion = property("MyCOVersion");
const protocolVersion = Number(property("MyCOProtocolVersion"));
const configSchemaVersion = Number(property("MyCOConfigSchemaVersion"));
const calibrationSchemaVersion = Number(
  property("MyCOCalibrationSchemaVersion")
);

// Bundle the TypeScript runtime into one self-contained script embedded by the WPF project.
await build({
  entryPoints: ["src/index.ts"],
  outfile: "dist/MyCO.runtime.js",
  bundle: true,
  platform: "browser",
  format: "iife",
  target: ["chrome120"],
  minify: true,
  legalComments: "none",
  sourcemap: false,
  define: {
    __MYCO_VERSION__: JSON.stringify(runtimeVersion),
    __MYCO_PROTOCOL_VERSION__: JSON.stringify(protocolVersion),
    __MYCO_CONFIG_SCHEMA_VERSION__: JSON.stringify(configSchemaVersion),
    __MYCO_CALIBRATION_SCHEMA_VERSION__: JSON.stringify(
      calibrationSchemaVersion
    )
  },
  banner: {
    js: `/* MyCO Skin Runtime v${runtimeVersion} — MIT */`
  }
});
