import { readFileSync } from "node:fs";
import { resolve } from "node:path";

// Unit tests execute TypeScript output directly instead of through esbuild, so
// expose the same values that build.mjs normally substitutes at bundle time.
const props = readFileSync(
  resolve(process.cwd(), "../../eng/MyCO.Version.props"),
  "utf8"
);

function property(name: string): string {
  const match = props.match(new RegExp(`<${name}>([^<]+)</${name}>`));
  if (!match?.[1]) throw new Error(`Missing ${name} in shared version props.`);
  return match[1].trim();
}

Object.assign(globalThis, {
  __MYCO_VERSION__: property("MyCOVersion"),
  __MYCO_PROTOCOL_VERSION__: Number(property("MyCOProtocolVersion")),
  __MYCO_CONFIG_SCHEMA_VERSION__: Number(
    property("MyCOConfigSchemaVersion")
  ),
  __MYCO_CALIBRATION_SCHEMA_VERSION__: Number(
    property("MyCOCalibrationSchemaVersion")
  )
});
