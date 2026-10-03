from pathlib import Path
import secrets
p=Path(__file__).resolve().parents[1]/'.env'
if p.exists():
    raise SystemExit('.env already exists; preserving it')
s=(p.parent/'.env.example').read_text()
for old in ['replace-with-long-random-database-password','replace-with-at-least-32-random-characters','replace-with-long-random-private-service-key']:
    s=s.replace(old,secrets.token_urlsafe(40))
p.write_text(s)
p.chmod(0o600)
print('Created .env with random keys and fictional demo password')
