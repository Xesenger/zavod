using System;

namespace zavod.Prompting;

internal static class PromptTransportSerializer
{
    public static PromptTransportPacket Serialize(PromptRequestPacket packet)
    {
        ArgumentNullException.ThrowIfNull(packet);

        var roleCoreText = PromptAssembler.RenderRoleCore(packet.Request);
        var shiftContextText = PromptAssembler.RenderShiftContext(packet.Request);
        var taskBlockText = PromptAssembler.RenderTaskBlock(packet.Request);
        var serializedAnchors = PromptAnchorSerializer.Serialize(packet.Request.Anchors);
        var anchorPackText = PromptAnchorSerializer.Render(serializedAnchors);

        return new PromptTransportPacket(
            packet.Role,
            packet.TruthMode,
            roleCoreText,
            shiftContextText,
            taskBlockText,
            serializedAnchors,
            anchorPackText,
            packet.Metadata);
    }
}
