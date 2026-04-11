\# ZAVOD - Roles Layer



Status: locked



\## Purpose



This folder contains role-truth documents for ZAVOD.



These files define:

\- role responsibility

\- role boundary

\- role input expectations

\- role output expectations

\- escalation relationships between roles



Role truth lives here.



\## Rule



```text

canon defines system laws

roles define role behavior

That means:



docs/zavod/canon/ = system truth

roles/ = role truth



These layers must not compete with each other.



Current Roles

1\. Shift Lead



Shift Lead is the interpretation and framing role.



Shift Lead:



interprets user intent

guides discussion toward executable framing

validates readiness for execution

defines bounded scope

protects project truth before execution starts



Shift Lead does not execute implementation work.



File:



shift\_lead.md

2\. Worker



Worker is the grounded execution role.



Worker:



executes validated intent

works inside current task scope

produces implementation result

stays inside truth and scope boundaries



Worker does not validate intent or mutate project truth.



File:



worker.md

3\. QC



QC is the verification role.



QC:



reviews result after execution

checks result against intent, scope, anchors, and project truth

returns accept / revise / reject style outcome



QC is not the same thing as Execution Verification Pipeline.



File:



qc.md

4\. Senior Specialist



Senior Specialist is the difficult-case support role.



Senior Specialist:



joins through escalation

analyzes hard technical or cross-system problems

surfaces risks and trade-offs

recommends safer next moves



Senior Specialist is not the default execution mode.



File:



senior\_specialist.md

Role Order in Normal Flow



Normal flow is:



User

→ Shift Lead

→ Worker

→ Execution Verification Pipeline

→ QC (if used)

→ User decision



Senior Specialist enters only through justified escalation.



Boundary Rules

Shift Lead vs Worker

Shift Lead defines:

what should be done

what the scope is

whether execution is ready

Worker defines:

how to implement inside that frame

Worker vs QC

Worker executes

QC verifies



Worker does not self-certify final correctness.



Shift Lead vs QC

Shift Lead frames the work

QC verifies the result



If the result reveals a framing problem,

QC may escalate back upward.



Senior Specialist vs everyone else



Senior Specialist supports:



Shift Lead in hard framing / analysis cases

Worker in deep technical cases

QC when verification reveals non-trivial structural issues



Senior Specialist does not become the default replacement for any normal role.



Escalation Map

Worker -> Shift Lead



Escalate when:



issue is truth-level

issue is decision-level

scope is contradictory

task framing is unsafe

canon conflict appears

QC -> Shift Lead



Escalate when:



validated intent is inconsistent

acceptance criteria conflict with project truth

issue is framing-level, not execution-level

Shift Lead -> Senior Specialist



Escalate when:



deeper technical analysis is required

cross-system risk is high

several plausible solutions require serious trade-off analysis

Worker -> Senior Specialist



Only through explicit escalation path,

not as a default shortcut.



Important Distinction

role != model

role = responsibility



A role is a system responsibility contract,

not merely “which model is used”.



Forbidden Pattern



The system must not blur roles into one generic “agent”.



Forbidden behavior:



Shift Lead silently doing Worker work

Worker silently doing Lead framing

QC silently becoming architecture owner

Senior Specialist becoming permanent default executor

Rules

role truth lives in roles/

canon truth lives in docs/zavod/canon/

roles must stay clearly separated

escalation must remain explicit

the system must prefer normal flow before specialist escalation

Canons

roles are responsibility contracts

roles must not overlap silently

roles/ is the role-truth layer

canon and roles must complement each other, not compete

