import { readFileSync } from "node:fs";
import { resolve } from "node:path";

// Unit tests execute TypeScript output directly instead of through esbuild, so
// expose the same values that build.mjs normally substitutes at bundle time.
const props = readFileSync(
  resolve(process.cwd(), "../../eng/MyCodex.Version.props"),
  "utf8"
);

function property(name: string): string {
  const match = props.match(new RegExp(`<${name}>([^<]+)</${name}>`));
  if (!match?.[1]) throw new Error(`Missing ${name} in shared version props.`);
  return match[1].trim();
}

Object.assign(globalThis, {
  __MYCODEX_VERSION__: property("MyCodexVersion"),
  __MYCODEX_PROTOCOL_VERSION__: Number(property("MyCodexProtocolVersion")),
  __MYCODEX_CONFIG_SCHEMA_VERSION__: Number(
    property("MyCodexConfigSchemaVersion")
  ),
  __MYCODEX_CALIBRATION_SCHEMA_VERSION__: Number(
    property("MyCodexCalibrationSchemaVersion")
  )
});
