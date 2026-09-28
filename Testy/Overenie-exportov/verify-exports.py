import sys, os
from pathlib import Path
from PIL import Image
import numpy as np, subprocess, re, zipfile, xml.etree.ElementTree as E, io, json
p=Path(sys.argv[1])
subprocess.run([os.environ.get('NODE_BINARY','node'),str(Path(__file__).with_name('render-svg.cjs')),str(p)],check=True)
report=[]
def compare(a,b,name):
 a=np.asarray(a.convert('RGB')).mean(2)<128;b=np.asarray(b.convert('RGB')).mean(2)<128
 assert a.shape==b.shape,(name,a.shape,b.shape)
 union=(a|b).sum();iou=(a&b).sum()/union if union else 1
 def dilate(mask):
  pad=np.pad(mask,1);return np.logical_or.reduce([pad[y:y+mask.shape[0],x:x+mask.shape[1]] for y in range(3) for x in range(3)])
 coverage=min((a&dilate(b)).sum()/max(1,a.sum()),(b&dilate(a)).sum()/max(1,b.sum()))
 area=abs(int(a.sum())-int(b.sum()))/max(1,a.sum())
 report.append((name,float(iou),float(coverage),float(area)));assert coverage>.99 and area<.05,(name,iou,coverage,area)
for pdf in p.glob('*.pdf'):
 base=pdf.with_suffix('');reference=Image.open(base.with_suffix('.png')).convert('RGB');w,h=reference.size
 subprocess.run(['pdftoppm','-singlefile','-png','-scale-to-x',str(w),'-scale-to-y',str(h),str(pdf),str(base)+'-pdf'],check=True,stdout=subprocess.DEVNULL,stderr=subprocess.PIPE)
 compare(reference,Image.open(str(base)+'-pdf.png'),base.name+' PDF')
 compare(reference,Image.open(str(base)+'-svg.png'),base.name+' SVG')
 mono=Image.open(str(base)+'-mono.png').convert('RGB');zpl=base.with_suffix('.zpl').read_text();bits=np.zeros((h,((w+7)//8)*8),dtype=np.uint8)
 for m in re.finditer(r'\^FO0,(\d+)\^GFA,(\d+),\d+,(\d+),([0-9A-F]+)\^FS',zpl):
  y,count,stride=int(m[1]),int(m[2]),int(m[3]);raw=bytes.fromhex(m[4]);assert len(raw)==count;chunk=np.unpackbits(np.frombuffer(raw,dtype=np.uint8)).reshape(-1,stride*8);bits[y:y+len(chunk),:]=chunk
 expected=np.asarray(mono).mean(2)<128;assert np.array_equal(expected,bits[:,:w].astype(bool)),base.name+' ZPL pixels';report.append((base.name+' ZPL',1.0))
 # Reconstruct Word's separately positioned PNG fallbacks at their documented physical bounds.
 with zipfile.ZipFile(base.with_suffix('.docx')) as z:
  xml=E.fromstring(z.read('word/document.xml'));ns={'wp':'http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing','a':'http://schemas.openxmlformats.org/drawingml/2006/main'}
  composite=Image.new('RGBA',(w,h),'white')
  rels=E.fromstring(z.read('word/_rels/document.xml.rels'));mapping={e.attrib['Id']:e.attrib['Target'] for e in rels}
  for anchor in xml.findall('.//wp:anchor',ns):
   x=int(anchor.find('wp:positionH/wp:posOffset',ns).text);y=int(anchor.find('wp:positionV/wp:posOffset',ns).text);ext=anchor.find('wp:extent',ns);cx=int(ext.attrib['cx']);cy=int(ext.attrib['cy']);blip=anchor.find('.//a:blip',ns);rid=blip.attrib['{http://schemas.openxmlformats.org/officeDocument/2006/relationships}embed'];img=Image.open(io.BytesIO(z.read('word/'+mapping[rid]))).convert('RGBA');scale=300/914400;img=img.resize((max(1,round(cx*scale)),max(1,round(cy*scale))),Image.Resampling.LANCZOS);composite.alpha_composite(img,(round(x*scale),round(y*scale)))
  composite.convert('RGB').save(str(base)+'-word.png');compare(reference,composite,base.name+' Word PNG fallback')
(p/'export-parity.json').write_text(json.dumps(report,indent=2));print(json.dumps(report,indent=2))
