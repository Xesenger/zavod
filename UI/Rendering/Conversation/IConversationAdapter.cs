using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;

namespace zavod.UI.Rendering.Conversation;

public interface IConversationAdapter
{
    ObservableCollection<ConversationItemViewModel> Items { get; }

    ConversationCapabilities Capabilities { get; }

    Task SendAsync(string text, CancellationToken cancellationToken = default);

    Task CancelAsync(string itemId, CancellationToken cancellationToken = default);

    Task RetryAsync(string itemId, CancellationToken cancellationToken = default);
}
