#!/usr/bin/env python3
"""Runs the balance acceptance checks (acceptance.js) headless and prints the report.

    python3 Tools/BalanceDashboard/acceptance.py                         # all levels, Medium, singles/pairs/triples
    python3 Tools/BalanceDashboard/acceptance.py --difficulty Easy --difficulty Hard --sizes 1 2
    python3 Tools/BalanceDashboard/acceptance.py --level "Beginner Level 01" --out report.md

Needs balance-data.js (run extract.py first) and JavaScriptCore (macOS) or node.
A full run plays a few hundred simulated games and takes several minutes.
"""
import argparse
import json
import os
import shutil
import subprocess
import tempfile

HERE = os.path.dirname(os.path.abspath(__file__))
JSC = '/System/Library/Frameworks/JavaScriptCore.framework/Versions/Current/Helpers/jsc'


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument('--level', action='append', help='level name (default: all playable levels)')
    ap.add_argument('--difficulty', action='append', help='Easy, Medium, Hard or Impossible (default: Medium)')
    ap.add_argument('--sizes', type=int, nargs='+', default=[1, 2, 3], help='palette sizes to sweep')
    ap.add_argument('--seeds', type=int, default=3, help='full-roster runs per level')
    ap.add_argument('--skip-static', action='store_true', help='skip the upgrade-curve checks')
    ap.add_argument('--out', help='also write the report to this file')
    args = ap.parse_args()

    opts = {'levels': args.level, 'difficulties': args.difficulty, 'sizes': args.sizes, 'seeds': args.seeds, 'skipStatic': args.skip_static}
    with tempfile.TemporaryDirectory() as tmp:
        shim = os.path.join(tmp, 'shim.js')
        with open(shim, 'w') as fh:
            fh.write('var window = this; if (typeof print === "undefined") { var print = function (s) { console.log(s); }; }\n')
            fh.write('var ACCEPTANCE_OPTS = %s;\n' % json.dumps({k: v for k, v in opts.items() if v is not None}))
        files = [shim] + [os.path.join(HERE, f) for f in ('balance-data.js', 'engine.js', 'acceptance.js')]
        if os.path.exists(JSC):
            res = subprocess.run([JSC] + files, capture_output=True, text=True)
        elif shutil.which('node'):
            res = subprocess.run(['node', '-e', ''.join(open(f).read() + '\n' for f in files)], capture_output=True, text=True)
        else:
            raise SystemExit('Neither JavaScriptCore nor node found')
    print(res.stdout, end='')
    if res.returncode != 0:
        raise SystemExit(res.stderr)
    if args.out:
        with open(args.out, 'w') as fh:
            fh.write(res.stdout)


if __name__ == '__main__':
    main()
