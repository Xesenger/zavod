\# ZAVOD - Canon Layer



Status: locked



\## Purpose



This folder contains system-truth documents for ZAVOD.



These files define:

\- system laws

\- lifecycle rules

\- truth boundaries

\- document boundaries

\- execution boundaries

\- persistence and resume boundaries

\- prompting and grounding system layers



Canon truth lives here.



\## Rule



```text

canon defines system laws

roles define role behavior

That means:



docs/zavod/canon/ = system truth

roles/ = role truth



These layers must not compete with each other.



What Canon Contains



Canon may define:



lifecycle

execution flow

validation boundaries

truth ownership

persistence rules

resume rules

prompting structure

anchor / grounding rules

verification layers

context construction rules

observation boundaries

runtime extension boundaries



Canon does not define:



personal writing style of a role

role-specific prompt personality

model choice by itself

user conversation wording as truth

Core Canon Principle

system truth is explicit, layered, and bounded



That means:



truth must have a clear owner

layers must not silently overlap

execution must not redefine truth

UI must not invent truth

runtime must not mutate truth directly

derived layers must remain below truth

Current Canon Areas

1\. Project Truth and Storage



Defines:



what active truth is

where truth lives

how .zavod/ is structured

what belongs to project documents vs meta vs history



Examples:



project\_truth\_storage\_layout\_v1.md

project\_state\_model\_v1.md

project\_state\_persistence\_v1.md

project\_meta\_contract\_v1.md

project\_state\_builder\_v1.md

2\. Intent / Interaction Boundary



Defines:



how intent appears

how intent is refined

when validation is allowed

how interaction differs from execution



Examples:



intent\_system\_v1.md

interaction\_validation\_flow\_v1.md

bootstrap\_flow\_v1.md

3\. Shift / Task / Snapshot Lifecycle



Defines:



how shifts start

how tasks live inside shifts

how closure works

how snapshot is formed

how history differs from active truth



Examples:



first\_shift\_creation\_v1.md

shift\_lifecycle\_v1.md

task\_model\_v1.md

shift\_closure\_review\_v1.md

snapshot\_model\_v1.md

current\_shift\_trace\_v1.md

4\. Execution Layer



Defines:



how execution starts

what boundaries execution obeys

why execution does not mutate truth directly

how result boundary differs from closure



Examples:



execution\_loop\_work\_cycle\_v1.md

read\_before\_write\_v1.md

worker\_execution\_strategy\_v1.md

execution\_verification\_pipeline\_v1.md

5\. Resume / Reconstruction Layer



Defines:



what resume may restore honestly

how reconstruction differs from memory

why fake continuity is forbidden



Examples:



resume\_contract\_v1.md

resume\_intelligence\_v1.md

6\. Prompting / Grounding Layer



Defines:



how execution requests are assembled

how anchors ground execution

how structured input replaces raw accumulated chat



Examples:



prompt\_assembly\_v1.md

anchor\_system\_v1.md

context\_builder\_v1.md

7\. Meta Support Layers



Defines:



helper layers below truth

observation boundaries

runtime-only support extensions



Examples:



observation\_layer\_v1.md

runtime\_execution\_extensions\_v1.md

Relation to Roles



Canon defines the system within which roles operate.



Examples:



canon defines that execution requires validated intent

roles define how Shift Lead and Worker behave inside that rule

canon defines that verification exists before Result UI

roles define what QC does on top of that system



So:



canon = system contract

roles = role contract

Relation to UI



Canon is above UI.



UI may:



project canon truth

reflect runtime state

expose valid actions



UI may not:



invent truth

bypass lifecycle

redefine project laws

become the source of canonical meaning

Relation to Runtime



Canon is above runtime.



Runtime may:



execute

verify

signal

optimize

isolate work



Runtime may not:



redefine truth

bypass closure

silently promote derived state into truth

Relation to Derived Layers



Derived layers exist below truth.



Examples:



cache

observations

tool memory

runtime signals

entry packs



Derived layers may support the system,

but must not replace truth.



Merge Rule



Canon files should be merged by system concept, not by filename alone.



Correct merge style:



execution with execution

resume with resume

prompting with prompting

roles with roles

truth storage with truth storage



Incorrect merge style:



“same name means same truth”

Promotion Rule



New draft material may enter canon only if:



it defines a real system boundary or law

it does not duplicate stronger existing canon

it has a clear place in the layer map

it does not compete with roles/

Forbidden Pattern



Canon must not become:



a chat dump

a note archive

a second role folder

a pile of overlapping files that say the same thing differently



If two canon files compete for the same truth,

they must be merged or one must be removed.



Working Rule



When adding new system features:



idea

→ draft

→ merge by concept

→ canon



Not:



idea

→ immediate canon duplication

Canons

docs/zavod/canon/ is the system-truth layer

canon defines laws, boundaries, and system structure

roles define role behavior in roles/

UI, runtime, and derived layers remain below canon truth

canon must stay explicit, layered, and non-competing

