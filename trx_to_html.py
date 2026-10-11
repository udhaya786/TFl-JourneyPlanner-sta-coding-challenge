#!/usr/bin/env python3
"""Convert a .trx test result file into a single self-contained, styled HTML report.

Usage:  python3 trx_to_html.py [path/to/results.trx] [output.html]
Default: newest .trx under ./TestResults  ->  ./TestResults/test-report.html
"""
import sys, glob, os, re, html, datetime
import xml.etree.ElementTree as ET

NS = {"t": "http://microsoft.com/schemas/VisualStudio/TeamTest/2010"}
KEYWORDS = ("Given ", "When ", "Then ", "And ", "But ")

def newest_trx():
    files = glob.glob("TestResults/**/*.trx", recursive=True)
    if not files:
        sys.exit("No .trx file found. Run: dotnet test --logger trx --results-directory ./TestResults")
    return max(files, key=os.path.getmtime)

def fmt_duration(d):
    try:
        h, m, s = d.split(":")
        return f"{int(h) * 3600 + int(m) * 60 + float(s):.1f} s"
    except Exception:
        return d or ""

def pretty_feature(class_name):
    short = (class_name or "").split(".")[-1]
    short = re.sub(r"Feature$", "", short)
    # Friendly names for this project's features
    if "TfL" in short or "Journey" in short:
        return "TfL Journey Planner (Web)"
    if "Additional" in short:
        return "Additional scenarios (documented, not automated)"
    if "OpenLibrary" in short:
        return "Open Library Books API"
    # Generic fallback: split CamelCase, keeping acronyms together
    spaced = re.sub(r"(?<=[a-z])(?=[A-Z])|(?<=[A-Z])(?=[A-Z][a-z])", " ", short)
    return spaced or "Tests"

def parse_steps(stdout):
    """Reqnroll writes 'Given ...' then '-> done: ...' / '-> error: ...' lines."""
    steps, current = [], None
    for line in (stdout or "").splitlines():
        s = line.strip()
        if not s:
            continue
        if s.startswith(KEYWORDS):
            current = {"text": s, "status": "unknown", "note": ""}
            steps.append(current)
        elif s.startswith("->") and current is not None:
            word = s[2:].strip().split(":")[0].split(" ")[0].lower()
            current["status"] = {"done": "pass", "error": "fail", "skipped": "skip",
                                 "pending": "skip"}.get(word, "unknown")
            if current["status"] in ("fail", "skip"):
                current["note"] = s[:300]
    return steps

trx = sys.argv[1] if len(sys.argv) > 1 else newest_trx()
out = sys.argv[2] if len(sys.argv) > 2 else os.path.join(os.path.dirname(trx) or ".", "test-report.html")
root = ET.parse(trx).getroot()

feature_of, cats_of = {}, {}
for u in root.iterfind(".//t:TestDefinitions/t:UnitTest", NS):
    tm = u.find("t:TestMethod", NS)
    feature_of[u.get("id")] = pretty_feature(tm.get("className") if tm is not None else "")
    cats_of[u.get("id")] = {c.get("TestCategory", "").lower()
                            for c in u.iterfind("t:TestCategory/t:TestCategoryItem", NS)}

results = []
for r in root.iterfind(".//t:Results/t:UnitTestResult", NS):
    msg = r.find(".//t:ErrorInfo/t:Message", NS)
    so = r.find(".//t:Output/t:StdOut", NS)
    stdout = (so.text or "") if so is not None else ""
    results.append({
        "feature": feature_of.get(r.get("testId"), "Tests"),
        "cats": cats_of.get(r.get("testId"), set()),
        "name": r.get("testName", ""),
        "outcome": r.get("outcome", ""),
        "duration": fmt_duration(r.get("duration", "")),
        "message": (msg.text or "").strip() if msg is not None else "",
        "steps": parse_steps(stdout),
        "stdout": stdout.strip(),
    })

passed = sum(1 for x in results if x["outcome"] == "Passed")
failed = sum(1 for x in results if x["outcome"] == "Failed")
other = len(results) - passed - failed          # not executed (documented, manual)
executed = passed + failed
pct = round(100 * passed / executed) if executed else 0
cls = lambda o: {"Passed": "pass", "Failed": "fail"}.get(o, "skip")
label = lambda o: {"Passed": "Passed", "Failed": "Failed"}.get(o, "Not automated")
ICON = {"pass": "✔", "fail": "✘", "skip": "●", "unknown": "·"}
def scrub(s):
    """Hide personal paths (usernames) in the report."""
    s = re.sub(r"(/Users|/home)/[^/\s]+", "~", s)
    s = re.sub(r"[A-Za-z]:\\Users\\[^\\\s]+", "~", s)
    return s

esc = lambda s: html.escape(scrub(s))

def tags_for(x):
    c, tags = x["cats"], set()
    if c & {"ui", "web"}: tags.add("ui")
    if c & {"api", "openlibrary"}: tags.add("api")
    if not tags:                                   # fall back to the feature name
        if "TfL" in x["feature"]: tags.add("ui")
        elif "Open Library" in x["feature"]: tags.add("api")
    if "functional" in c: tags.add("functional")
    if "non-functional" in c: tags.add("non-functional")
    tags.add("executed" if x["outcome"] in ("Passed", "Failed") else "manual")
    return tags

CHIP = {"ui": "UI", "api": "API", "functional": "Functional", "non-functional": "Non-functional"}
for x in results:
    x["tags"] = tags_for(x)

sections = []
for feature in sorted({x["feature"] for x in results}, key=lambda f: (f.startswith("Additional"), f)):
    items = [x for x in results if x["feature"] == feature]
    fp = sum(1 for x in items if x["outcome"] == "Passed")
    ex = sum(1 for x in items if x["outcome"] in ("Passed", "Failed"))
    na = len(items) - ex
    summary_txt = (f"{fp}/{ex} passed" if ex else "") + (" &middot; " if ex and na else "") + (f"{na} not automated" if na else "")
    cards = []
    for x in items:
        step_html = "".join(
            f"<li class='step {s['status']}'><span class='ico'>{ICON[s['status']]}</span>{esc(s['text'])}"
            + (f"<div class='note'>{esc(s['note'])}</div>" if s["note"] else "") + "</li>"
            for s in x["steps"])
        err = f"<h4>Error</h4><pre>{esc(x['message'][:3000])}</pre>" if x["message"] else ""
        body = f"<ul class='steps'>{step_html}</ul>{err}" if (step_html or err) else ""
        chips = "".join(f"<span class='chip {k}'>{CHIP[k]}</span>"
                        for k in ("ui", "api", "functional", "non-functional") if k in x["tags"])
        tag_attr = " ".join(sorted(x["tags"]))
        cards.append(
            f"<details class='test {cls(x['outcome'])}' data-tags='{tag_attr}'{' open' if x['outcome'] == 'Failed' else ''}>"
            f"<summary><span class='badge {cls(x['outcome'])}'>{esc(label(x['outcome']))}</span>"
            f"<span class='tname'>{esc(x['name'])}</span><span class='chips'>{chips}</span>"
            f"<span class='dur'>{esc(x['duration'])}</span></summary>"
            f"{body}</details>")
    sections.append(
        f"<section><h2>{esc(feature)} <small>{summary_txt}</small></h2>{''.join(cards)}</section>")

count = lambda tag: sum(1 for x in results if tag in x["tags"])
FILTERS = [("all", "All", len(results)), ("ui", "UI", count("ui")), ("api", "API", count("api")),
           ("functional", "Functional", count("functional")),
           ("non-functional", "Non-functional", count("non-functional")),
           ("manual", "Not automated", count("manual"))]
filters_html = "".join(
    f"<button data-f='{k}' class='{'on' if k == 'all' else ''}'>{lbl} <em>{n}</em></button>"
    for k, lbl, n in FILTERS)
generated = datetime.datetime.now().strftime("%d %b %Y %H:%M")
page = f"""<!DOCTYPE html>
<html lang="en"><head><meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>Test report</title>
<style>
 *{{box-sizing:border-box}}
 body{{font-family:-apple-system,"Segoe UI",Roboto,Helvetica,Arial,sans-serif;margin:0;background:#f4f6fa;color:#1c1e21}}
 header{{background:linear-gradient(135deg,#1f2a44,#34508a);color:#fff;padding:28px 32px}}
 header h1{{margin:0 0 6px;font-size:24px}} header p{{margin:0;opacity:.8;font-size:13px}}
 main{{max-width:1000px;margin:-24px auto 40px;padding:0 16px}}
 .cards{{display:flex;gap:12px;flex-wrap:wrap;margin-bottom:24px}}
 .card{{flex:1;min-width:130px;background:#fff;border-radius:12px;padding:16px 20px;box-shadow:0 2px 6px rgba(20,30,60,.1)}}
 .card b{{display:block;font-size:30px}} .card span{{font-size:11px;color:#667;text-transform:uppercase;letter-spacing:.06em}}
 .card.pass b{{color:#1a7f37}} .card.fail b{{color:#cf222e}}
 .bar{{height:8px;background:#ffd8d3;border-radius:4px;overflow:hidden;margin-top:8px}}
 .bar i{{display:block;height:100%;background:#2da44e;width:{pct}%}}
 section{{margin-bottom:26px}}
 h2{{font-size:17px;margin:0 0 10px}} h2 small{{font-weight:400;color:#667;font-size:13px;margin-left:8px}}
 .test{{background:#fff;border-radius:10px;margin-bottom:8px;box-shadow:0 1px 3px rgba(20,30,60,.08);border-left:5px solid #2da44e}}
 .test.fail{{border-left-color:#cf222e}} .test.skip{{border-left-color:#d4a72c}}
 summary{{list-style:none;cursor:pointer;display:flex;align-items:center;gap:12px;padding:12px 16px}}
 summary::-webkit-details-marker{{display:none}}
 .tname{{flex:1;font-size:14px;font-weight:500}} .dur{{color:#667;font-size:12px}}
 .badge{{padding:2px 10px;border-radius:12px;font-size:11px;font-weight:700;text-transform:uppercase}}
 .badge.pass{{background:#dafbe1;color:#1a7f37}} .badge.fail{{background:#ffebe9;color:#cf222e}} .badge.skip{{background:#fff8c5;color:#9a6700}}
 .steps{{list-style:none;margin:0;padding:4px 20px 14px 20px}}
 .step{{font-size:13px;padding:4px 0;color:#333}} .ico{{display:inline-block;width:22px;font-weight:700}}
 .step.pass .ico{{color:#1a7f37}} .step.fail{{color:#cf222e}} .step.fail .ico{{color:#cf222e}} .step.skip .ico{{color:#9a6700}}
 .note{{margin:2px 0 0 22px;font-size:12px;color:#888;word-break:break-word}}
 .chips{{display:flex;gap:6px}}
 .chip{{padding:1px 8px;border-radius:10px;font-size:11px;font-weight:600;background:#eef0f4;color:#445}}
 .chip.ui{{background:#e3f0ff;color:#0b5cad}} .chip.api{{background:#f1e8ff;color:#6a3fb5}}
 .chip.functional{{background:#e6f6ec;color:#17692f}} .chip.non-functional{{background:#fff1dc;color:#9a5b00}}
 .filters{{display:flex;gap:8px;flex-wrap:wrap;margin:0 0 20px}}
 .filters button{{border:1px solid #cfd6e4;background:#fff;border-radius:18px;padding:6px 14px;font-size:13px;cursor:pointer;color:#334}}
 .filters button em{{font-style:normal;color:#889;margin-left:4px}}
 .filters button.on{{background:#1f2a44;color:#fff;border-color:#1f2a44}} .filters button.on em{{color:#cdd6ee}}
 h4{{margin:8px 20px 4px;font-size:12px;color:#667;text-transform:uppercase}}
 pre{{margin:0 20px 16px;background:#0d1117;color:#e6edf3;padding:12px;border-radius:6px;overflow:auto;max-height:260px;font-size:12px;white-space:pre-wrap}}
</style></head><body>
<header><h1>Automated test report</h1>
<p>Generated {generated}</p></header>
<main>
 <div class="cards">
  <div class="card"><b>{executed}</b><span>Executed</span></div>
  <div class="card pass"><b>{passed}</b><span>Passed</span></div>
  <div class="card fail"><b>{failed}</b><span>Failed</span></div>
  <div class="card"><b>{other}</b><span>Not automated</span></div>
  <div class="card"><b>{pct}%</b><span>Pass rate</span><div class="bar"><i></i></div></div>
 </div>
 <div class="filters">{filters_html}</div>
 {''.join(sections)}
</main>
<script>
document.querySelectorAll('.filters button').forEach(function(b){{
  b.addEventListener('click', function(){{
    document.querySelectorAll('.filters button').forEach(function(x){{x.classList.remove('on');}});
    b.classList.add('on');
    var f = b.dataset.f;
    document.querySelectorAll('.test').forEach(function(t){{
      t.style.display = (f === 'all' || t.dataset.tags.split(' ').indexOf(f) > -1) ? '' : 'none';
    }});
    document.querySelectorAll('section').forEach(function(s){{
      var any = Array.prototype.some.call(s.querySelectorAll('.test'), function(t){{return t.style.display !== 'none';}});
      s.style.display = any ? '' : 'none';
    }});
  }});
}});
</script>
</body></html>"""

with open(out, "w", encoding="utf-8") as f:
    f.write(page)
print(f"Report written to {out}  ({passed} passed, {failed} failed, {other} other)")
