import pytest
from app import AnalysisInput, ModelOutput, validate_and_ground, ExtractInput, extract_records

def data():
    return AnalysisInput.model_validate({"requirements": [{"skill_id":"python","name":"Python","proficiency":2}], "evidence":[{"id":"f1","skill_id":"python","value":"Python","quote":"Skills: Python","section_id":"page-1","verification":"Unverified"}]})

def test_no_invented_evidence():
    with pytest.raises(ValueError):
        validate_and_ground(data(), ModelOutput.model_validate({"selections":[{"skill_id":"python","evidence_ids":["made-up"]}]}))

def test_unverified_not_documented():
    out = validate_and_ground(data(), ModelOutput.model_validate({"selections":[{"skill_id":"python","evidence_ids":["f1"]}]}))
    assert out.gaps[0].status == "Unverified"
    assert out.gaps[0].proficiency_met is None

def test_omission_rejected():
    with pytest.raises(ValueError):
        validate_and_ground(data(), ModelOutput.model_validate({"selections":[{"skill_id":"python","evidence_ids":[]}]}))

def test_missing_not_inability():
    d = data(); d.evidence = []
    out = validate_and_ground(d, ModelOutput.model_validate({"selections":[{"skill_id":"python","evidence_ids":[]}]}))
    assert out.gaps[0].status == "Missing"
    assert "not absence of skill" in out.gaps[0].explanation

def test_no_hiring_attributes():
    with pytest.raises(ValueError):
        ModelOutput.model_validate({"selections":[], "recommendation":"hire"})

def test_alias_boundaries_and_source():
    d=ExtractInput.model_validate({"sections":[{"id":"page-1","text":"Skills: Python3\nEducation: BSc fictional university\npythonista"}],"skills":[{"id":"python","name":"Python","aliases":["Python3"]}]})
    facts=extract_records(d)
    assert len([f for f in facts if f["kind"]=="skill"])==1
    assert all(f["section_id"]=="page-1" for f in facts)
