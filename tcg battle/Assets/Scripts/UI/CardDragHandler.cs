using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace TcgBattle.UI
{
    /// <summary>
    /// Drag events for a hand CardView only. It is added to hand cards by
    /// HandController, so board cards, the opponent's hidden hand and deck backs
    /// never become draggable. Disable the component when dragging is not legal.
    /// </summary>
    public class CardDragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public CardView View { get; private set; }
        public event Action<CardView, PointerEventData> Started;
        public event Action<CardView, PointerEventData> Moved;
        public event Action<CardView, PointerEventData> Ended;

        public void Bind(CardView view)
        {
            View = view;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (!isActiveAndEnabled || View == null || View.Card == null)
                return;
            Started?.Invoke(View, eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!isActiveAndEnabled || View == null || View.Card == null)
                return;
            Moved?.Invoke(View, eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!isActiveAndEnabled || View == null || View.Card == null)
                return;
            Ended?.Invoke(View, eventData);
        }
    }
}
