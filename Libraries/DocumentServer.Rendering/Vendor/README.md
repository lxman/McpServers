# Vendored code — `PdfLibrary.Rendering.SkiaSharp`

This directory is **third-party source, copied verbatim.** Do not edit it to fix bugs
in this repo's code; fix upstream, or the copy will silently diverge.

| | |
|---|---|
| Upstream | https://github.com/lxman/PdfLibrary |
| Path | `PdfLibrary.Rendering.SkiaSharp/` |
| Commit | `e781e05c1e595f1f4173d24664706d72c0dd76d9` (2026-09-06) |
| License | MIT |
| Files | 13 (`.cs`), 2,219 lines |
| Local modifications | **None.** Source is byte-identical to upstream. |

## Why this is vendored rather than referenced

The upstream project is *sunset*: it sets `IsPackable=false`, and the last version
published to NuGet was 1.1.0, now unlisted, while the core is at 2.6.0. It is not
abandoned — it is a live solution project that drives upstream's pixel-fidelity tests
against the current core — it is simply not shipped as a package.

It is viable to vendor because it is **absent from the core's `InternalsVisibleTo`
list**, so it compiles against the public NuGet surface of `Lxman.PdfLibrary` with no
privileged access.

## What this code does and does not do

It is a **pixel sink only.** There is no PDF parser and no content-stream interpreter
here. All PDF work — parsing, content-stream interpretation, image decoding
(JBIG2/JPEG/CCITT), colour-space resolution, MRC compositing, `/Rotate` — is done by the
`Lxman.PdfLibrary` core, which emits draw calls into an `IRenderTarget`. These files
implement that interface against a Skia canvas.

## Constraint

Requires **SkiaSharp 4.151.2 or later**; `SKPathBuilder` does not exist in 3.x. The pin
lives in `../PdfRender.csproj`.

## Status

The publish-vs-vendor decision is **deliberately deferred** until after the OCR engine
bake-off. See `artifacts/render_probe.json` for the options and the evidence. If
upstream publishes a 2.6.x package, delete this directory and add a `PackageReference`.
