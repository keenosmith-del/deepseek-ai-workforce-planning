import os
from pathlib import Path
from fastapi.testclient import TestClient
from app import app
os.environ['INTERNAL_API_KEY']='test-private-key'
client=TestClient(app)
headers={'X-Internal-Key':'test-private-key'}

def test_private_service():
    assert client.post('/parse',content=b'bad').status_code==401

def test_invalid_document_rejected():
    assert client.post('/parse',content=b'PKnot-a-zip',headers=headers).status_code==422

def test_pdf_docx_source_sections():
    root=Path('/fixtures')
    if not root.exists(): root=Path(__file__).resolve().parents[2]/'demo'
    for name in ['fictional-resume.pdf','fictional-resume.docx']:
        r=client.post('/parse',content=(root/name).read_bytes(),headers=headers)
        assert r.status_code==200
        assert any('C#' in s['text'] for s in r.json()['sections'])

def test_macro_archive_rejected():
    import io,zipfile
    data=io.BytesIO()
    with zipfile.ZipFile(data,'w') as z:
        z.writestr('word/document.xml','<doc/>');z.writestr('word/vbaProject.bin','x')
    assert client.post('/parse',content=data.getvalue(),headers=headers).status_code==422
