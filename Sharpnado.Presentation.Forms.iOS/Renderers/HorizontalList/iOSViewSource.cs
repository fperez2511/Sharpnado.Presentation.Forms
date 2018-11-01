﻿using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;

using Foundation;
using Sharpnado.Presentation.Forms.RenderedViews;
using UIKit;
using Xamarin.Forms;
using Xamarin.Forms.Platform.iOS;

namespace Sharpnado.Presentation.Forms.iOS.Renderers.HorizontalList
{
    public struct UIViewCellHolder
    {
        public static UIViewCellHolder Empty = new UIViewCellHolder(null, null);

        public UIViewCellHolder(ViewCell formsCell, UIView view)
        {
            FormsCell = formsCell;
            CellContent = view;
        }

        public ViewCell FormsCell { get; }

        public UIView CellContent { get; }

        public static bool operator ==(UIViewCellHolder c1, UIViewCellHolder c2)
        {
            return ReferenceEquals(c1.FormsCell, c2.FormsCell) && ReferenceEquals(c1.CellContent, c2.CellContent);
        }

        public static bool operator !=(UIViewCellHolder c1, UIViewCellHolder c2)
        {
            return !ReferenceEquals(c1.FormsCell, c2.FormsCell) || !ReferenceEquals(c1.CellContent, c2.CellContent);
        }

        public bool Equals(UIViewCellHolder other)
        {
            return Equals(FormsCell, other.FormsCell) && Equals(CellContent, other.CellContent);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
            {
                return false;
            }

            return obj is UIViewCellHolder holder && Equals(holder);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((FormsCell != null ? FormsCell.GetHashCode() : 0) * 397) ^ (CellContent != null ? CellContent.GetHashCode() : 0);
            }
        }

    }

    public class iOSViewCell : UICollectionViewCell
    {
        public iOSViewCell(IntPtr p)
            : base(p)
        {
        }

        public bool IsInitialized => FormsCell != null;

        public ViewCell FormsCell { get; private set; }

        public void Initialize(ViewCell formsCell, UIView view)
        {
            FormsCell = formsCell;
            ContentView.AddSubview(view);
        }

        public void Bind(object dataContext)
        {
            FormsCell.BindingContext = dataContext;
        }
    }

    public class iOSViewSource : UICollectionViewDataSource
    {
        private readonly WeakReference<HorizontalListView> _weakElement;

        private readonly List<object> _dataSource;
        private readonly UIViewCellHolderQueue _viewCellHolderCellHolderQueue;

        public iOSViewSource(HorizontalListView element)
        {
            _weakElement = new WeakReference<HorizontalListView>(element);

            var elementItemsSource = element.ItemsSource;
            _dataSource = elementItemsSource?.Cast<object>().ToList();

            if (_dataSource == null)
            {
                return;
            }

            if (!(element.ItemTemplate is DataTemplateSelector))
            {
                // Cache only support single DataTemplate
                _viewCellHolderCellHolderQueue = new UIViewCellHolderQueue(
                    element.ViewCacheSize,
                    () => CreateViewCellHolder());
                _viewCellHolderCellHolderQueue.Build();
            }
        }

        public void HandleNotifyCollectionChanged(NotifyCollectionChangedEventArgs eventArgs)
        {
            switch (eventArgs.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    _dataSource.InsertRange(eventArgs.NewStartingIndex, eventArgs.NewItems.Cast<object>());
                    break;
                case NotifyCollectionChangedAction.Remove:
                    _dataSource.RemoveRange(eventArgs.OldStartingIndex, eventArgs.OldItems.Count);
                    break;
                case NotifyCollectionChangedAction.Reset:
                    _dataSource.Clear();
                    break;
                case NotifyCollectionChangedAction.Move:
                    var item = _dataSource[eventArgs.OldStartingIndex];
                    _dataSource.RemoveAt(eventArgs.OldStartingIndex);
                    _dataSource.Insert(eventArgs.NewStartingIndex, item);
                    break;
            }
        }

        public override bool CanMoveItem(UICollectionView collectionView, NSIndexPath indexPath) =>
            _weakElement.TryGetTarget(out var element) && element.EnableDragAndDrop;

        public override void MoveItem(UICollectionView collectionView, NSIndexPath sourceIndexPath, NSIndexPath destinationIndexPath)
        {
            var item = _dataSource[(int)sourceIndexPath.Item];

            _dataSource.RemoveAt((int)sourceIndexPath.Item);
            _dataSource.Insert((int)destinationIndexPath.Item, item);
        }

        public override nint NumberOfSections(UICollectionView collectionView)
        {
            return 1;
        }

        public override nint GetItemsCount(UICollectionView collectionView, nint section)
        {
            return _dataSource?.Count ?? 0;
        }

        public override UICollectionViewCell GetCell(UICollectionView collectionView, NSIndexPath indexPath)
        {
            var nativeCell = (iOSViewCell)collectionView.DequeueReusableCell(nameof(iOSViewCell), indexPath);

            if (!nativeCell.IsInitialized)
            {
                UIViewCellHolder holder;
                if (_weakElement.TryGetTarget(out var element) && element.ItemTemplate is DataTemplateSelector)
                {
                    holder = CreateViewCellHolder(indexPath.Row);
                }
                else
                {
                    holder = _viewCellHolderCellHolderQueue.Dequeue();
                }

                if (holder == UIViewCellHolder.Empty)
                {
                    return nativeCell;
                }

                nativeCell.Initialize(holder.FormsCell, holder.CellContent);
            }

            nativeCell.Bind(_dataSource[indexPath.Row]);

            return nativeCell;
        }

        protected override void Dispose(bool disposing)
        {
            _viewCellHolderCellHolderQueue?.Clear();

            base.Dispose(disposing);
        }

        private UIViewCellHolder CreateViewCellHolder(int itemIndex = -1)
        {
            ViewCell formsCell = null;

            if (!_weakElement.TryGetTarget(out var element))
            {
                return UIViewCellHolder.Empty;
            }

            if (element.ItemTemplate is DataTemplateSelector selector)
            {
                if (itemIndex == -1)
                {
                    throw new InvalidOperationException("Cannot select a DataTemplate without an itemIndex");
                }

                var template = selector.SelectTemplate(_dataSource[itemIndex], element.Parent);
                formsCell = (ViewCell)template.CreateContent();
            }
            else
            {
                formsCell = (ViewCell)element.ItemTemplate.CreateContent();
            }

            formsCell.Parent = element;
            formsCell.View.Layout(new Rectangle(0, 0, element.ItemWidth, element.ItemHeight));

            if (Platform.GetRenderer(formsCell.View) == null)
            {
                IVisualElementRenderer renderer = Platform.CreateRenderer(formsCell.View);
                Platform.SetRenderer(formsCell.View, renderer);
            }

            var nativeView = Platform.GetRenderer(formsCell.View).NativeView;
            nativeView.ContentMode = UIViewContentMode.ScaleAspectFit;

            return new UIViewCellHolder(formsCell, nativeView);
        }
    }
}