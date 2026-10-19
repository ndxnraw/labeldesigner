import sys, os
from pathlib import Path
import zipfile,subprocess,numpy as np,json
from PIL import Image
p=Path(sys.argv[1]);parts=p/'word-parts';parts.mkdir(exist_ok=True)
for f in p.glob('*.docx'):
 with zipfile.ZipFile(f) as z:
  for n in z.namelist():
   if n.startswith('word/media/'):(parts/(f.stem+'-'+Path(n).name)).write_bytes(z.read(n))
subprocess.run([os.environ.get('NODE_BINARY','node'),str(Path(__file__).with_name('render-word-svg.cjs')),str(parts)],check=True)
report=[]
for f in parts.glob('*.svg'):
 def mask(p):
  im=Image.open(p).convert('RGBA');bg=Image.new('RGBA',im.size,'white');bg.alpha_composite(im);return np.array(bg.convert('RGB')).mean(2)<128
 a=mask(f.with_suffix('.png'));b=mask(f.with_name(f.stem+'-svg.png'))
 def expand(a):
  p=np.pad(a,1);return np.logical_or.reduce([p[y:y+a.shape[0],x:x+a.shape[1]] for y in range(3) for x in range(3)])
 score=min((a&expand(b)).sum()/max(1,a.sum()),(b&expand(a)).sum()/max(1,b.sum()));report.append([f.name,float(score)]);assert score>.99,(f.name,score)
(p/'word-svg-parity.json').write_text(json.dumps(report,indent=2));print('Word SVG/PNG:',len(report),'parts; minimum coverage within one pixel:',min(x[1] for x in report))
