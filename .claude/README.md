# Why this directory exists

For most of this repository's life there was no `.claude/` here at all, and nobody noticed,
because a session that touched Explorer was almost always started from the Munin checkout next
door (`Fhi.Metadata`) and loaded *its* `.claude/` instead. Steering written for a .NET API with a
React client, a bilingual changelog, i18n JSON and EF migrations is not merely useless in a
Razor Class Library that has none of those — it is confidently wrong, and it has already cost
something visible: thirty-two merged pull requests here carried a bare `Closes #N`, which closed
nothing at all, because Munin's instructions of the time assumed the issue lived in the same
repository. The files here exist so a session opened *in this folder*
gets steering that names things that exist in this folder. They stay deliberately thin:
[`AGENTS.md`](../AGENTS.md) is canonical for the conventions themselves and the skills point at
it rather than restating it, because a second copy of a rule is a rule that will eventually
disagree with the first one — and the one people read is whichever they happen to open.
