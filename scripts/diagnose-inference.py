"""Run inside AI container. Prints only opaque IDs from genuine model output."""
import asyncio
import json
import app
from pathlib import Path
original=app.ollama
async def observed(path,payload,timeout=180):
    payload["think"]=False
    payload.setdefault("options",{})["num_thread"]=4
    r=await original(path,payload,timeout)
    if path=='/api/generate':
        print(json.dumps({'response':r.get('response'),'done_reason':r.get('done_reason'),'tokens':r.get('eval_count'),'thinking_length':len(r.get('thinking',''))}),flush=True)
    return r
app.ollama=observed
needs=[{'skill_id':str(i),'name':name,'proficiency':2} for i,name in enumerate(['C#','PostgreSQL','Docker'],1)]
evidence=[{'id':str(i),'skill_id':str(sk),'value':'Skill claim','quote':'Fictional skill claim','section_id':'page-1','verification':'Unverified'} for i,sk in enumerate([1,2,3,1,2,3,3],1)]
try:
    data=app.AnalysisInput.model_validate_json(Path('/input.json').read_text()) if Path('/input.json').exists() else app.AnalysisInput(requirements=needs,evidence=evidence)
    print(asyncio.run(app.analyze(data)))
except Exception as e:
    print(type(e).__name__,str(e))
