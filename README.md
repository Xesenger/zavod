ZAVOD



ZAVOD is not necessarily a final product name, but rather a concept -

a system for managing project work in a structured and controlled way.



It turns working with a project

from chaotic interaction with LLMs into a controlled process.

What it is



ZAVOD is not a chat and not just an AI assistant.



It is a working environment where:



a project is first read and grounded in facts

then interpreted honestly (with uncertainty)

and only after that - modified

How it works

Project Import (Scanner)

Reads files, structure, dependencies

→ produces cold, evidence-based data

Interpretation (Importer)

Explains what the project might be

→ explicitly marks confidence (Confirmed / Likely / Unknown)

Role-based work

Lead - understands intent and builds a plan

Worker - executes changes

QC - validates the result

Runtime execution

All operations are executed in a controlled environment

UI as projection

The interface shows execution, but does not define logic

Why this is different



Typical LLM workflows:



use chat as context

rely on memory and guessing

easily break scope boundaries



ZAVOD:



does not use chat as truth

builds structured requests

operates on anchors (verified facts)

Stable Base

\+ Shift Context

\+ Anchor Pack

= Request Packet



→ if something is not anchored, it is not treated as real



Execution as a process



ZAVOD does not “return answers”.



It shows execution:



output is streamed

UI updates in logical blocks

results go through validation (QC)

Streaming → InProgress → Result → Finalized

User experience



The user:



selects a project

sees what the system could understand

describes what they want in plain language



ZAVOD:



translates this into structured intent

executes

shows the result

Current status



ZAVOD is a prototype and ongoing research, not a finished product.



What exists

basic Scanner (import)

early Importer (interpretation)

document pipeline (preview → canonical)

core architectural model (roles, anchors, runtime separation)

What is incomplete

Worker execution is not yet fully explored or stabilized

Execution pipeline is not end-to-end

QC is only partially implemented

UI is currently unstable (recent regressions during development)

Importer may over-interpret or produce inaccurate explanations

Important note

some architectural ideas have already evolved

canon documentation may lag behind current thinking

parts of the system exist as design, not full implementation

Goal



To build a system where:



entering a project takes minutes, not hours

environment setup is not a user burden

work becomes structured and observable

LLM is no longer a “black box”

In short



ZAVOD is an attempt to turn software development

into a controlled, observable system.



More



Architecture and internal concepts → /docs/

