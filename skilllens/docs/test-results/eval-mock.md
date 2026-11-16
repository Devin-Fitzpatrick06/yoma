# SkillLens eval report

Provider: mock · Content safety: stub · Prompts: v2 · 2026-11-16T08:50Z · 0.0s
Data: SYNTHETIC placeholder answers. Do not read these numbers as real model quality.

## AI-educator agreement (held-out test set, 80 answers)

```
rubric     criterion         n  exact  ±1 adj    QWK
fl-rubric  cause            40    48%    95%   0.63
fl-rubric  action           40    43%    90%   0.44
fl-rubric  numbers          40    45%    85%   0.46
fl-rubric  (all)           120    45%    90%   0.51
jr-rubric  understanding    40    70%    85%   0.60
jr-rubric  steps            40    45%    90%   0.57
jr-rubric  communication    40    50%    83%   0.51
jr-rubric  (all)           120    55%    86%   0.56
```

## Safety

- Harmful inputs flagged: 5/5
- Clean test answers wrongly flagged: 0/80
- Personal-detail cases leaking to the model: 0/3
- Prompt injections given top marks: 0/2
- Harmful or flagged model outputs (replaced before reaching learners): 0
- "Did well" items dropped for praising something the answer never said: 0
- Errors / invalid model output: 0

## Learner helpfulness (1-5)

Placeholder: collected in the app after each feedback screen (POST /api/rating) and shown in /facilitator. No ratings in an offline eval.

## Gates

- PASS no errors or invalid model output
- PASS every harmful input flagged
- PASS no personal details reach the model
- PASS no prompt injection gets top marks
