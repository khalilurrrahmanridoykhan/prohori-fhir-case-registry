#!/usr/bin/env python3
import base64, subprocess, pathlib

HERE = pathlib.Path(__file__).parent
SB = HERE.parent
OUT = pathlib.Path("/Users/khalilur/Documents/AIWORK/prohori-fhir-case-registry/docs/linkedin")
OUT.mkdir(parents=True, exist_ok=True)
CHROME = "/Applications/Google Chrome.app/Contents/MacOS/Google Chrome"

def b64(p):
    return base64.b64encode(pathlib.Path(p).read_bytes()).decode()

FRAME = """<!doctype html>
<html><head><meta charset="utf-8" />
<link rel="stylesheet" href="base.css" />
<style>
  .body {{ padding: 44px 52px 44px; gap: 20px; }}
  .cap {{ font-family: var(--sans); font-size: 22px; font-weight: 600; letter-spacing: -0.01em; color: var(--ink); }}
  .cap .m {{ font-family: var(--mono); font-size: 15px; font-weight: 400; color: var(--muted); display: block; margin-top: 6px; letter-spacing: 0; }}
  .shot {{
    flex: 1; border: 1px solid var(--border); border-radius: 8px; overflow: hidden;
    background: {shotbg};
    box-shadow: 0 30px 60px -30px rgba(0,0,0,.6);
  }}
  .shot img {{ width: 100%; height: 100%; object-fit: cover; object-position: top center; display: block; }}
  .foot {{ padding-top: 20px; }}
</style></head>
<body>
  <div class="board">
    <div class="chrome">
      <span class="dots"><i></i><i></i><i></i></span>
      <span class="tab">{tab}</span>
      <span class="chrome-right">{chromeright}</span>
    </div>
    <div class="body">
      <div class="cap">{cap}<span class="m">{sub}</span></div>
      <div class="shot"><img src="data:image/png;base64,{img}" alt="" /></div>
      <div class="foot"><span class="repo">prohori-fhir-case-registry.vercel.app</span></div>
    </div>
  </div>
</body></html>
"""

boards = {
    "3-dashboard.html": FRAME.format(
        tab="dashboard", chromeright="reads the DGHS sandbox, live",
        cap="The surveillance dashboard",
        sub="React 19 · reads sandbox.fhir.dghs.gov.bd directly — search, filters, positivity",
        shotbg="#f4f2ee", img=b64(SB / "live-dash-light.png")),
}
for name, html in boards.items():
    (HERE / name).write_text(html)

RENDER = [
    ("1-hero.html", "prohori-li-1-hero.png"),
    ("2-result.html", "prohori-li-2-result.png"),
    ("3-dashboard.html", "prohori-li-3-dashboard.png"),
]
for src, dst in RENDER:
    r = subprocess.run([
        CHROME, "--headless=new", "--disable-gpu", "--hide-scrollbars",
        "--force-device-scale-factor=2", "--window-size=1200,1200",
        "--virtual-time-budget=6000",
        f"--screenshot={OUT / dst}", f"file://{HERE / src}",
    ], capture_output=True, text=True)
    line = [l for l in (r.stderr or "").splitlines() if "written" in l.lower()]
    print(dst, "→", line[0] if line else "(check)")
