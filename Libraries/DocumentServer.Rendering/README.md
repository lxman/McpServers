# DocumentServer.Rendering

Rasterises PDF pages to images so they can be OCR'd.

## Why this exists

`Lxman.PdfLibrary` does all the PDF work — parsing, content-stream interpretation,
image decoding (JBIG2 / CCITT / JPEG / JPEG 2000), colour-space resolution, MRC
compositing, `/Rotate` — but it deliberately ships **no rasteriser**. It emits draw
calls into an `IRenderTarget` and leaves the pixels to someone else. The only
implementations in the package are `RecordingRenderTarget`, which produces no pixels,
and `PdfImageToRgba`, which decodes a single image XObject.

That is not enough to OCR a page. On this corpus 58% of scanned pages carry two or
more image XObjects (67 carry five or more), so decoding them individually yields
disconnected fragments that cut straight through text lines. Nothing applies the CTM
either, so rotated and flipped fax scans come out in the wrong orientation, and any
vector or text overlay — stamps, signatures, redactions — is lost entirely.

So `Skia/` supplies the canvas: it is the pixel sink the core draws into.

## Layout

| Path | Contents |
|---|---|
| `PdfPageRasterizer.cs` | The entry point. Render at a DPI, optionally supersampled. |
| `Skia/` | `IRenderTarget` implemented against an `SKCanvas`. |
| `Skia/Conversion/` | PDF → Skia value mapping (paths, colours, blend modes). |
| `Skia/Renderers/` | Per-operation drawing: images, paths, patterns, shadings. |
| `Skia/State/` | Graphics-state and soft-mask stacks. |

## Provenance

`Skia/` began as `PdfLibrary.Rendering.SkiaSharp` from
[lxman/PdfLibrary](https://github.com/lxman/PdfLibrary) (MIT) at commit
`e781e05c1e595f1f4173d24664706d72c0dd76d9`. That project was never shipped as a
package — it set `IsPackable=false`, and the last NuGet release was 1.1.0, unlisted,
while the core moved on to 2.6.x. It has since been retired upstream, and this copy
is now the maintained one. Edit it freely; there is nothing left to diverge from.

The namespaces were renamed from `PdfLibrary.Rendering.SkiaSharp.*` to
`DocumentServer.Rendering.Skia.*` on adoption. The rendering logic was not otherwise
altered, and output was confirmed pixel-identical afterwards.

## Constraints

- **SkiaSharp 4.151.2 or later.** `SKPathBuilder` does not exist in 3.x.
- **Keep the native package version aligned with the managed one.** ShapeCrawler pulls
  `SkiaSharp.NativeAssets.Linux` 4.150.1, whose `libSkiaSharp.so` the 4.151.2 managed
  assembly refuses to load. Both packages deliver the same native path, so the older
  one can win. This fails only at runtime and only on Linux, so the build will not warn
  you. The pin lives in `Directory.Packages.props`.
- **Do not "simplify" the supersampled downsample.** It must area-average, which is why
  it asks for mipmap sampling. Scanned pages hold bilevel CCITT at roughly 200 DPI;
  point-sampling them shreds glyph strokes and Tesseract then returns zero bytes with
  exit code 0. See the remarks on `PdfPageRasterizer.RenderSupersampled`.
