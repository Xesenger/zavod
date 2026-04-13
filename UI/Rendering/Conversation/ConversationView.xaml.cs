using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Collections.Specialized;

namespace zavod.UI.Rendering.Conversation;

public sealed partial class ConversationView : UserControl
{
    public ConversationView()
    {
        InitializeComponent();
        Loaded += ConversationView_Loaded;
    }

    private void ConversationView_Loaded(object sender, RoutedEventArgs e)
    {
        RefreshItems();
    }

    public IConversationAdapter? Adapter
    {
        get => (IConversationAdapter?)GetValue(AdapterProperty);
        set => SetValue(AdapterProperty, value);
    }

    public static readonly DependencyProperty AdapterProperty =
        DependencyProperty.Register(
            nameof(Adapter),
            typeof(IConversationAdapter),
            typeof(ConversationView),
            new PropertyMetadata(null, OnAdapterChanged));

    private static void OnAdapterChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((ConversationView)d).ApplyAdapter(e.OldValue as IConversationAdapter, e.NewValue as IConversationAdapter);
    }

    private void ApplyAdapter(IConversationAdapter? oldAdapter, IConversationAdapter? newAdapter)
    {
        if (oldAdapter?.Items is INotifyCollectionChanged oldItems)
        {
            oldItems.CollectionChanged -= Items_CollectionChanged;
        }

        MessagesRepeater.ItemsSource = newAdapter?.Items;

        if (newAdapter?.Items is INotifyCollectionChanged newItems)
        {
            newItems.CollectionChanged += Items_CollectionChanged;
        }

        if (IsLoaded)
        {
            RefreshItems();
        }
    }

    public void RefreshItems()
    {
        if (!IsLoaded)
        {
            return;
        }

        var itemsSource = Adapter?.Items;
        MessagesRepeater.ItemsSource = itemsSource;
        MessagesRepeater.InvalidateMeasure();
        MessagesRepeater.InvalidateArrange();
    }

    private void Items_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            if (!IsLoaded)
            {
                return;
            }

            MessagesRepeater.InvalidateMeasure();
            MessagesRepeater.InvalidateArrange();
            ConversationScrollViewer.ChangeView(null, ConversationScrollViewer.ScrollableHeight, null, true);
        });
    }
}
