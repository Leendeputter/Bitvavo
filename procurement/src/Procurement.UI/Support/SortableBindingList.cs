using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace Procurement.UI.Support
{
    /// <summary>
    /// A BindingList that a DataGridView can sort by clicking a column header (user request, 17 nov
    /// 2026: "de optie om de kolommen te sorteren in Purchase Requests"). A plain List or BindingList
    /// has no sort support, so the grid ignores header clicks. Works for anonymous row types too, via
    /// <see cref="SortableBindingList.Create{T}"/>. Empty values sort first ascending, last descending.
    /// </summary>
    public class SortableBindingList<T> : BindingList<T>
    {
        private bool _isSorted;
        private ListSortDirection _sortDirection;
        private PropertyDescriptor _sortProperty;

        public SortableBindingList(IList<T> items) : base(items) { }

        protected override bool SupportsSortingCore => true;
        protected override bool IsSortedCore => _isSorted;
        protected override ListSortDirection SortDirectionCore => _sortDirection;
        protected override PropertyDescriptor SortPropertyCore => _sortProperty;

        protected override void ApplySortCore(PropertyDescriptor property, ListSortDirection direction)
        {
            var items = (List<T>)Items;
            var sorted = direction == ListSortDirection.Ascending
                ? items.OrderBy(i => property.GetValue(i), ValueComparer.Instance).ToList()
                : items.OrderByDescending(i => property.GetValue(i), ValueComparer.Instance).ToList();

            items.Clear();
            items.AddRange(sorted);

            _sortProperty = property;
            _sortDirection = direction;
            _isSorted = true;
            OnListChanged(new ListChangedEventArgs(ListChangedType.Reset, -1));
        }

        protected override void RemoveSortCore()
        {
            _isSorted = false;
            _sortProperty = null;
        }

        private sealed class ValueComparer : IComparer<object>
        {
            public static readonly ValueComparer Instance = new ValueComparer();

            public int Compare(object x, object y)
            {
                if (x == null || x == DBNull.Value) return y == null || y == DBNull.Value ? 0 : -1;
                if (y == null || y == DBNull.Value) return 1;
                if (x is string xs && y is string ys)
                    return string.Compare(xs, ys, StringComparison.CurrentCultureIgnoreCase);
                if (x is IComparable comparable && x.GetType() == y.GetType())
                    return comparable.CompareTo(y);
                return string.Compare(x.ToString(), y.ToString(), StringComparison.CurrentCultureIgnoreCase);
            }
        }
    }

    public static class SortableBindingList
    {
        /// <summary>Type inference helper, so an anonymous row type can be wrapped.</summary>
        public static SortableBindingList<T> Create<T>(IEnumerable<T> items) => new SortableBindingList<T>(items.ToList());
    }
}
