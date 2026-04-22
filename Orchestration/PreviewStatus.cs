using System.Collections.Generic;
using zavod.Persistence;

namespace zavod.Orchestration;

// Preview-stage document state for the Work Packet preview_status field
// (see project_work_packet_v1.md).
//
// Present only when canonical 5/5 is not yet reached. Lists which kinds
// exist at Preview stage so the model surface can honestly mark them
// as below-canonical content.
//
// PreviewKinds is intentionally a bounded kind list; the packet does not
// carry preview bodies here — those flow via Capsule source_stage and
// explicit preview document reads performed downstream.
public sealed record PreviewStatus(IReadOnlyList<ProjectDocumentKind> PreviewKinds);
