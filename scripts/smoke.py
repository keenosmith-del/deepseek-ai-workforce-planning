"""Real API integration journey. Requires seeded Compose stack; no simulated inference."""
import argparse
import json
import os
import time
import urllib.error
import urllib.request
from pathlib import Path
from datetime import datetime, timezone, timedelta

p=argparse.ArgumentParser();p.add_argument('--url',default='http://localhost:5180');p.add_argument('--require-ai',action='store_true');args=p.parse_args()
root=Path(__file__).resolve().parents[1]
password=os.getenv('DEMO_PASSWORD')
if not password:
 for line in (root/'.env').read_text().splitlines():
  if line.startswith('DEMO_PASSWORD='): password=line.split('=',1)[1]

def call(path,method='GET',body=None,token=None,expected=200,raw=False):
 headers={}
 if token:headers['Authorization']='Bearer '+token
 if body is not None and not raw: body=json.dumps(body).encode();headers['Content-Type']='application/json'
 if raw:body,ctype=body;headers['Content-Type']=ctype
 req=urllib.request.Request(args.url+'/api'+path,data=body,method=method,headers=headers)
 try:
  with urllib.request.urlopen(req,timeout=250) as r:code=r.status;data=r.read()
 except urllib.error.HTTPError as e:code=e.code;data=e.read()
 assert code==expected,(method,path,code,data[:500])
 return json.loads(data) if data and not raw else data

def login(role):return call('/auth/login','POST',{'email':role+'@demo.test','password':password})['token']
hr,recruiter,candidate,employee=[login(r) for r in ['hr','recruiter','candidate','employee']]
call('/departments','POST',{'name':'Forbidden department'},candidate,expected=403)
call('/dashboard',token=candidate,expected=403)
skills=call('/skills',token=hr);ids={s['name']:s['id'] for s in skills}
stamp=str(int(time.time()))
dep=call('/departments','POST',{'name':'Smoke engineering '+stamp},hr)
job=call('/jobs','POST',{'departmentId':dep['id'],'title':'Smoke platform '+stamp,'description':'Fictional evidence-based demonstration vacancy','open':True,'requirements':[{'skillId':ids['C#'],'proficiency':2},{'skillId':ids['PostgreSQL'],'proficiency':2},{'skillId':ids['Docker'],'proficiency':2}]},hr)
a=call('/jobs/'+job['id']+'/apply','POST',token=candidate);aid=a['id'];base='/applications/'+aid
other=call('/auth/register','POST',{'email':'other-'+stamp+'@demo.test','name':'Fictional isolation check','password':'FictionalOnly!123'})['token']
call(base,token=other,expected=404)
call(base+'/analysis','POST',token=candidate,expected=403)
call('/jobs/'+job['id']+'/apply','POST',token=candidate,expected=409)
fixture=(root/'demo/fictional-resume.docx').read_bytes();boundary='----workforce-test'
body=(f'--{boundary}\r\nContent-Disposition: form-data; name="file"; filename="fictional-resume.docx"\r\nContent-Type: application/vnd.openxmlformats-officedocument.wordprocessingml.document\r\n\r\n'.encode()+fixture+f'\r\n--{boundary}--\r\n'.encode())
call(base+'/documents','POST',(body,'multipart/form-data; boundary='+boundary),candidate,expected=202,raw=True)

def wait_processing(timeout=900):
 deadline=time.time()+timeout
 while time.time()<deadline:
  d=call(base,token=recruiter)
  if d['processing'] and all(j['status'] in ['Completed','Failed'] for j in d['processing']):return d
  time.sleep(2)
 raise AssertionError('Background processing timed out')
d=wait_processing();assert d['facts'] and d['documents'][0]['sections']!='[]'
if args.require_ai:assert d['analyses'],'No successful local AI inference'
else:assert d['analyses'] or any(j['kind']=='Analyze' and j['status']=='Failed' and j['error'] for j in d['processing'])
f=next(f for f in d['facts'] if f['skillId']==ids['C#']);version=d['application']['evidenceVersion']
correction={'evidenceVersion':version,'skillId':f['skillId'],'value':'C# API project','verification':'Documented','proficiency':2,'note':'Human reviewed exact source quotation'}
call(base+'/facts/'+f['id'],'PUT',correction,recruiter)
call(base+'/facts/'+f['id'],'PUT',correction,recruiter,expected=409)
d=call(base,token=recruiter);assert all(x['stale'] for x in d['analyses']);assert d['application']['evidenceVersion']==version+1
call(base+'/analysis','POST',token=recruiter,expected=202);d=wait_processing()
if args.require_ai:
 latest=d['analyses'][0];assert not latest['stale'];result=json.loads(latest['result']);assert next(g for g in result['gaps'] if g['skill_id']==ids['C#'])['status']=='Documented';assert json.loads(latest['metadata'])['model_digest']
 call('/skills/embeddings','POST',token=hr)
 matches=call('/skills/search?q=relational%20database%20SQL',token=recruiter);assert matches and any(m['name']=='PostgreSQL' for m in matches)
call(base+'/reviews','POST',{'note':'Evidence and uncertainty reviewed by fictional recruiter'},recruiter)
interview=call(base+'/interviews','POST',{'scheduledAt':(datetime.now(timezone.utc)+timedelta(days=2)).isoformat(),'location':'Fictional meeting room','notes':''},recruiter)
call(base+'/interviews/'+interview['id'],'PUT',{'scheduledAt':interview['scheduledAt'],'location':'Fictional meeting room','notes':'Discussed API project; manual notes only.'},recruiter)
call(base+'/status','PUT',{'status':'Hired','rationale':'Invalid bypass'},recruiter,expected=400)
for status in ['UnderReview','Interview','Offered','Hired']:
 call(base+'/status','PUT',{'status':status,'rationale':'Manually recorded fictional demonstration decision'},recruiter)
public=call(base,token=candidate);assert not public['analyses'] and not public['decisions'];assert public['interviews'][0]['notes'] is None
call('/profile/skills','PUT',{'skillId':ids['Docker'],'proficiency':2,'evidence':'Fictional container workshop','developmentNeed':'Advanced networking'},employee)
inventory=call('/profile',token=employee);entry=next(x for x in inventory if x['skillId']==ids['Docker']);assert entry['verification']=='Unverified'
call('/profile/employees/skills/'+entry['id']+'/verification','PUT',{'verification':'Documented'},hr)
dash=call('/dashboard',token=hr);assert any(s['name']=='Docker' and s['documentedEmployees']>=1 for s in dash['coverage'])
assert call('/audit',token=hr)
assert any(j['id']==job['id'] and not j['open'] for j in call('/jobs',token=hr))
call(base+'/documents/'+d['documents'][0]['id'],'DELETE',token=candidate,expected=204)
d=call(base,token=recruiter);assert not d['facts'] and not d['analyses'] and not d['reviews']
call(base,'DELETE',token=candidate,expected=204);call(base,token=recruiter,expected=404)
print('PASS: persisted recruitment journey, document parsing, source correction/versioning, RBAC/isolation, interviews, manual decisions, employee skills, dashboard, audit, deletion.')
print('Local AI + pgvector verified.' if args.require_ai else 'AI success or explicit unavailable failure verified; --require-ai is needed to assert inference and pgvector.')
