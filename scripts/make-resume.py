"""Generate fictional, portable DOCX and PDF fixtures without external libraries."""
from pathlib import Path
from zipfile import ZipFile, ZIP_DEFLATED
from html import escape
root=Path(__file__).resolve().parents[1]/'demo'
lines=['Taylor Candidate — FICTIONAL DEMONSTRATION ONLY', 'Education: BSc Software Engineering, Example University (fictional), 2022', 'Employment: Example Labs (fictional), Software Developer, 2022–2025', 'Experience: Built C# APIs with PostgreSQL for a fictional inventory project.', 'Technologies: C#, PostgreSQL, Python3, Docker', 'Skills: React, TypeScript', 'Project: Containerized services using Docker and wrote automated tests.']
with ZipFile(root/'fictional-resume.docx','w',ZIP_DEFLATED) as z:
 z.writestr('[Content_Types].xml','<?xml version="1.0"?><Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/word/document.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"/></Types>')
 z.writestr('_rels/.rels','<?xml version="1.0"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="word/document.xml"/></Relationships>')
 z.writestr('word/document.xml','<?xml version="1.0"?><w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main"><w:body>'+''.join('<w:p><w:r><w:t>'+escape(l)+'</w:t></w:r></w:p>' for l in lines)+'</w:body></w:document>')
# PDF fixture uses only ASCII text and a base font; original DOCX carries Unicode.
text='BT /F1 11 Tf 40 780 Td '+ ' '.join(('0 -26 Td ' if i else '')+'('+l.encode('ascii','replace').decode().replace('\\','\\\\').replace('(','\\(').replace(')','\\)')+') Tj' for i,l in enumerate(lines))+' ET'
objects=['<< /Type /Catalog /Pages 2 0 R >>','<< /Type /Pages /Kids [3 0 R] /Count 1 >>','<< /Type /Page /Parent 2 0 R /MediaBox [0 0 842 842] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>','<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>',f'<< /Length {len(text)} >>\nstream\n{text}\nendstream']
pdf=bytearray(b'%PDF-1.4\n');offsets=[0]
for i,obj in enumerate(objects,1):
 offsets.append(len(pdf));pdf.extend(f'{i} 0 obj\n{obj}\nendobj\n'.encode())
xref=len(pdf);pdf.extend(f'xref\n0 {len(objects)+1}\n0000000000 65535 f \n'.encode())
for off in offsets[1:]: pdf.extend(f'{off:010d} 00000 n \n'.encode())
pdf.extend(f'trailer\n<< /Size {len(objects)+1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n'.encode());(root/'fictional-resume.pdf').write_bytes(pdf)
print('Generated fictional PDF/DOCX resumes')
