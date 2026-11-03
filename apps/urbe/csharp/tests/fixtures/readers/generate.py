"""Original deterministic reading corpus. Requires python-pptx, python-docx, Pillow; no downloaded user content."""
from pathlib import Path
from io import BytesIO
import zipfile
from pptx import Presentation
from pptx.util import Inches, Pt
from pptx.dml.color import RGBColor
from pptx.enum.shapes import MSO_SHAPE
from docx import Document
from PIL import Image, ImageDraw, ImageFont
ROOT=Path(__file__).parent
image=Image.new('RGB',(1400,700),'white');draw=ImageDraw.Draw(image)
font=ImageFont.truetype('/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf',48)
draw.text((45,60),'Estudo local: leitura e conhecimento.',fill='black',font=font)
draw.text((45,145),'Texto original 2026. Formula: E = mc2.',fill='black',font=font)
draw.rectangle((45,260,600,600),outline='#168383',width=8)
draw.text((85,390),'Figura de teste',fill='black',font=font)
image.save(ROOT/'scan.png')
def canonical(path):
 with zipfile.ZipFile(path) as z: entries={i.filename:z.read(i) for i in z.infolist()}
 with zipfile.ZipFile(path,'w',zipfile.ZIP_DEFLATED) as z:
  for name,data in sorted(entries.items()):
   info=zipfile.ZipInfo(name,(2026,1,1,0,0,0));info.compress_type=zipfile.ZIP_DEFLATED;z.writestr(info,data)
p=Presentation();p.slide_width=Inches(12);p.slide_height=Inches(6.75)
s=p.slides.add_slide(p.slide_layouts[6]);box=s.shapes.add_textbox(Inches(.5),Inches(.4),Inches(10),Inches(1));box.text='Estudo de apresentações';box.text_frame.paragraphs[0].runs[0].font.size=Pt(32)
shape=s.shapes.add_shape(MSO_SHAPE.ROUNDED_RECTANGLE,Inches(.6),Inches(1.6),Inches(4),Inches(2));shape.fill.solid();shape.fill.fore_color.rgb=RGBColor(22,131,131);shape.text='Forma e texto preservados'
s.shapes.add_picture(str(ROOT/'scan.png'),Inches(5),Inches(1.6),width=Inches(6))
table=s.shapes.add_table(2,2,Inches(.6),Inches(4.4),Inches(6),Inches(1)).table
for r,row in enumerate([['Fonte','Valor'],['Urbe','42']]):
 for c,text in enumerate(row):table.cell(r,c).text=text
s2=p.slides.add_slide(p.slide_layouts[6]);s2.shapes.add_textbox(Inches(1),Inches(1),Inches(10),Inches(2)).text='Segundo slide: trecho exato.'
p.save(ROOT/'slides.pptx');canonical(ROOT/'slides.pptx')
d=Document();d.add_heading('Leitura semântica',0);d.add_paragraph('Trecho original de estudo.',style=None).runs[0].bold=True;d.add_picture(str(ROOT/'scan.png'),width=Inches(4));t=d.add_table(rows=2,cols=2)
t.cell(0,0).text='Fonte';t.cell(0,1).text='Valor';t.cell(1,0).text='Urbe';t.cell(1,1).text='42';d.add_heading('Segunda seção',1);d.add_paragraph('Outra passagem verificável.');d.save(ROOT/'document.docx');canonical(ROOT/'document.docx')
with zipfile.ZipFile(ROOT/'comic.cbz','w',zipfile.ZIP_DEFLATED) as z:
 for i in [10,2,1]:z.writestr(zipfile.ZipInfo(f'page{i}.png',(2026,1,1,0,0,0)),(ROOT/'scan.png').read_bytes())
for ext in ['odt','odp']:
 with zipfile.ZipFile(ROOT/f'document.{ext}','w',zipfile.ZIP_DEFLATED) as z:
  z.writestr('mimetype','application/vnd.oasis.opendocument.'+('text' if ext=='odt' else 'presentation'))
  body='<text:p>Trecho ODF exato.</text:p><text:h>Seção de teste</text:h>'
  if ext=='odp':body='<draw:page draw:name="Primeiro"><draw:frame><draw:text-box>'+body+'</draw:text-box></draw:frame></draw:page>'
  z.writestr('content.xml','<office:document-content xmlns:office="urn:oasis:names:tc:opendocument:xmlns:office:1.0" xmlns:text="urn:oasis:names:tc:opendocument:xmlns:text:1.0" xmlns:draw="urn:oasis:names:tc:opendocument:xmlns:drawing:1.0"><office:body><office:'+('text' if ext=='odt' else 'presentation')+'>'+body+'</office:'+('text' if ext=='odt' else 'presentation')+'></office:body></office:document-content>')
 canonical(ROOT/f'document.{ext}')
print('Generated original reader corpus')
from reportlab.pdfgen import canvas
from reportlab.lib.pdfencrypt import StandardEncryption
for pages in [1,20,300,1001]:
 c=canvas.Canvas(str(ROOT/f'book-{pages}.pdf'),pagesize=(595,842),invariant=1)
 for i in range(pages):
  if i in [0,pages//2]:
   c.bookmarkPage(f'p{i}');c.addOutlineEntry('Início' if i==0 else 'Segunda parte',f'p{i}',0)
  c.setFont('Helvetica',16);c.drawString(45,785,f'Página {i+1}: fonte original de estudo.')
  if i==0:
   c.drawImage(str(ROOT/'scan.png'),45,380,width=505,height=252.5)
   c.linkURL('https://example.org/study',(45,340,250,365),relative=0)
   c.drawString(45,345,'Referência externa de teste')
  else:
   for line in range(20):c.drawString(45,730-line*25,f'Trecho verificável {i+1}.{line+1}. E = mc2.')
  c.showPage()
 c.save()
c=canvas.Canvas(str(ROOT/'scanned.pdf'),pagesize=(595,842),invariant=1);c.drawImage(str(ROOT/'scan.png'),0,272,width=595,height=297.5);c.showPage();c.save()
c=canvas.Canvas(str(ROOT/'protected.pdf'),invariant=1,encrypt=StandardEncryption('reader-test'));c.drawString(45,750,'Arquivo protegido de teste');c.showPage();c.save()
for version,fixed,name in [('3.0',False,'reflow.epub'),('2.0',False,'epub2.epub'),('3.0',True,'fixed.epub')]:
 with zipfile.ZipFile(ROOT/name,'w',zipfile.ZIP_DEFLATED) as z:
  def put(p,text):z.writestr(zipfile.ZipInfo(p,(2026,1,1,0,0,0)),text)
  put('mimetype','application/epub+zip')
  put('META-INF/container.xml','<container xmlns="urn:oasis:names:tc:opendocument:xmlns:container" version="1.0"><rootfiles><rootfile full-path="EPUB/book.opf" media-type="application/oebps-package+xml"/></rootfiles></container>')
  put('EPUB/book.opf','<package xmlns="http://www.idpf.org/2007/opf" version="'+version+'" unique-identifier="id"><metadata xmlns:dc="http://purl.org/dc/elements/1.1/"><dc:identifier id="id">urbe-reader-corpus</dc:identifier><dc:title>Leitura de prova</dc:title><dc:language>pt-BR</dc:language><meta property="dcterms:modified">2026-01-01T00:00:00Z</meta>'+('<meta property="rendition:layout">pre-paginated</meta>' if fixed else '')+'</metadata><manifest><item id="nav" href="nav.xhtml" media-type="application/xhtml+xml" properties="nav"/><item id="ncx" href="toc.ncx" media-type="application/x-dtbncx+xml"/><item id="one" href="one.xhtml" media-type="application/xhtml+xml"/><item id="two" href="two.xhtml" media-type="application/xhtml+xml"/><item id="image" href="image.png" media-type="image/png"/></manifest><spine toc="ncx"><itemref idref="one"/><itemref idref="two"/></spine></package>')
  put('EPUB/nav.xhtml','<html xmlns="http://www.w3.org/1999/xhtml" xmlns:epub="http://www.idpf.org/2007/ops"><head><title>Sumário</title></head><body><nav epub:type="toc"><ol><li><a href="one.xhtml">Primeira seção</a></li><li><a href="two.xhtml">Segunda seção</a></li></ol></nav></body></html>')
  put('EPUB/toc.ncx','<ncx xmlns="http://www.daisy.org/z3986/2005/ncx/" version="2005-1"><head><meta name="dtb:uid" content="urbe-reader-corpus"/></head><docTitle><text>Leitura de prova</text></docTitle><navMap><navPoint id="one" playOrder="1"><navLabel><text>Primeira seção</text></navLabel><content src="one.xhtml"/></navPoint><navPoint id="two" playOrder="2"><navLabel><text>Segunda seção</text></navLabel><content src="two.xhtml"/></navPoint></navMap></ncx>')
  put('EPUB/one.xhtml','<html xmlns="http://www.w3.org/1999/xhtml"><head><title>Primeira seção</title><meta name="viewport" content="width=595,height=842"/></head><body><h1>Primeira seção</h1>'+''.join(f'<p>Parágrafo {i}. Trecho estável para seleção e retomada após alterar tipografia.</p>' for i in range(50))+'<img src="image.png"/><p>Fórmula semântica <math xmlns="http://www.w3.org/1998/Math/MathML"><mi>E</mi><mo>=</mo><mi>m</mi><msup><mi>c</mi><mn>2</mn></msup></math></p><a href="two.xhtml#note">Nota de rodapé</a></body></html>')
  put('EPUB/two.xhtml','<html xmlns="http://www.w3.org/1999/xhtml"><head><title>Segunda seção</title></head><body><h1>Segunda seção</h1><p id="note">Texto da nota de rodapé.</p><p dir="rtl">مرحبا بالعالم</p><a href="one.xhtml">Voltar</a></body></html>')
  put('EPUB/image.png',(ROOT/'scan.png').read_bytes())
print('Generated PDF and EPUB corpus')
