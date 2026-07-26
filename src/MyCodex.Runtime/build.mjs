import { build } from "esbuild";

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
  banner: {
    js: "/* MyCodex Skin Runtime v0.1.1-alpha — MIT */"
  }
});
