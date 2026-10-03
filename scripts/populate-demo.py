"""Populate a reviewable fictional journey through real APIs (no fabricated AI).

The staff-token operations below are explicitly fictional human actions for the demo,
not model decisions. Run after Compose and optional local model installation.
"""
import json
import os
import time
import urllib.error
import urllib.request
from datetime import datetime, timedelta, timezone
from pathlib import Path

root = Path(__file__).resolve().parents[1]
base = os.getenv('WORKFORCE_URL', 'http://localhost:5180') + '/api'
password = os.getenv('DEMO_PASSWORD') or next(line.split('=', 1)[1] for line in (root / '.env').read_text().splitlines() if line.startswith('DEMO_PASSWORD='))

def request(path, token, method='GET', payload=None, content_type='application/json'):
    headers = {'Authorization': 'Bearer ' + token}
    if payload is not None:
        headers['Content-Type'] = content_type
        if content_type == 'application/json':
            payload = json.dumps(payload).encode()
    req = urllib.request.Request(base + path, data=payload, method=method, headers=headers)
    with urllib.request.urlopen(req, timeout=250) as response:
        text = response.read()
        return json.loads(text) if text else None

def login(role):
    return request('/auth/login', '', 'POST', {'email': role + '@demo.test', 'password': password})['token']

hr, recruiter, candidate = [login(r) for r in ['hr', 'recruiter', 'candidate']]
skills = {s['name']: s['id'] for s in request('/skills', hr)}
department = next(d for d in request('/departments', hr) if d['name'] == 'Engineering')
jobs = request('/jobs', hr)
job = next((j for j in jobs if j['title'] == 'Evidence review demonstration'), None)
if not job:
    job = request('/jobs', hr, 'POST', {'departmentId': department['id'], 'title': 'Evidence review demonstration', 'description': 'Fictional vacancy for inspecting document evidence, local analysis, human review and interview records.', 'open': True, 'requirements': [{'skillId': skills[name], 'proficiency': 2} for name in ['C#', 'PostgreSQL', 'Docker']]})
application = next((a for a in request('/applications', candidate) if a['jobId'] == job['id']), None)
if not application:
    application = request('/jobs/' + job['id'] + '/apply', candidate, 'POST')
path = '/applications/' + application['id']
detail = request(path, recruiter)
if not detail['documents']:
    boundary = '----workforce-fictional-demo'
    data = (f'--{boundary}\r\nContent-Disposition: form-data; name="file"; filename="fictional-resume.docx"\r\nContent-Type: application/vnd.openxmlformats-officedocument.wordprocessingml.document\r\n\r\n'.encode() + (root / 'demo/fictional-resume.docx').read_bytes() + f'\r\n--{boundary}--\r\n'.encode())
    request(path + '/documents', candidate, 'POST', data, 'multipart/form-data; boundary=' + boundary)

def wait():
    deadline = time.time() + 900
    while time.time() < deadline:
        d = request(path, recruiter)
        if d['processing'] and all(j['status'] in ['Completed', 'Failed'] for j in d['processing']):
            return d
        time.sleep(2)
    raise RuntimeError('Processing timed out; inspect the application in the UI')

detail = wait()
if not detail['facts']:
    raise RuntimeError('Extraction failed; inspect visible processing errors')
fact = next(f for f in detail['facts'] if f['skillId'] == skills['C#'])
if fact['verification'] != 'Documented':
    request(path + '/facts/' + fact['id'], recruiter, 'PUT', {'evidenceVersion': detail['application']['evidenceVersion'], 'skillId': fact['skillId'], 'value': 'C# API project documented in fictional resume', 'verification': 'Documented', 'proficiency': 2, 'note': 'FICTIONAL DEMO human review: inspected the source quotation and recorded level 2.'})
    request(path + '/analysis', recruiter, 'POST')
    detail = wait()
if not detail['reviews'] or not any(r['action'] == 'review.recorded' for r in detail['reviews']):
    request(path + '/reviews', recruiter, 'POST', {'note': 'FICTIONAL DEMO: source evidence inspected; unverified proficiency and uncertainty retained. No AI hiring recommendation.'})
status = detail['application']['status']
if status == 'Submitted':
    request(path + '/status', recruiter, 'PUT', {'status': 'UnderReview', 'rationale': 'Fictional human reviewer opened the evidence review.'})
    status = 'UnderReview'
if not detail['interviews']:
    interview = request(path + '/interviews', recruiter, 'POST', {'scheduledAt': (datetime.now(timezone.utc) + timedelta(days=3)).isoformat(), 'location': 'Fictional meeting room A', 'notes': 'FICTIONAL DEMO: discuss source-backed API project examples; these are human-entered notes.'})
if status == 'UnderReview':
    request(path + '/status', recruiter, 'PUT', {'status': 'Interview', 'rationale': 'Fictional recruiter manually scheduled an interview. No model decision.'})
print('Populated persisted demo application:', application['id'])
print('Fresh local AI analysis available.' if any(not a['stale'] for a in detail['analyses']) else 'AI is unavailable/failed; inspect the explicit processing state and retry after model setup.')
print('Open Applications as recruiter@demo.test to inspect source, corrections, analysis versions, reviews, interview notes and manual decisions.')
