"""Export a fictional demo application for private inference diagnostics."""
import json
import urllib.request
from pathlib import Path
root=Path(__file__).resolve().parents[1]
password=next(x.split('=',1)[1] for x in (root/'.env').read_text().splitlines() if x.startswith('DEMO_PASSWORD='))
def get(path,body=None,token=None):
 headers={'Content-Type':'application/json'}
 if token:headers['Authorization']='Bearer '+token
 r=urllib.request.Request('http://localhost:5180/api'+path,headers=headers,data=json.dumps(body).encode() if body else None)
 with urllib.request.urlopen(r) as response:return json.load(response)
token=get('/auth/login',{'email':'recruiter@demo.test','password':password})['token']
apps=get('/applications',token=token)
d=get('/applications/'+apps[0]['id'],token=token)
skills={s['id']:s['name'] for s in get('/skills',token=token)}
payload={'requirements':[{'skill_id':r['skillId'],'name':skills[r['skillId']],'proficiency':r['proficiency']} for r in d['job']['requirements']], 'evidence':[{'id':f['id'],'skill_id':f['skillId'],'value':f['value'],'quote':f['quote'],'section_id':f['documentId']+':'+f['sectionId'],'verification':f['verification'],'proficiency':f['proficiency']} for f in d['facts']]}
out=root/'artifacts/analysis-input.json';out.write_text(json.dumps(payload));out.chmod(0o600)
print('Exported private fictional diagnostic input')
