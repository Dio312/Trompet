using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public class CardFlip : MonoBehaviour
{
    [SerializeField] [Tooltip("Should the card flip when the mouse hovers over it?")]
    private bool flipOnMouseHover = false;
    [SerializeField]  [Tooltip("card shadow has a shadow that should be flipped with the card")] 
    private bool UseShadow = true;
    [SerializeField] private GameObject cardBack, cardFront;
    [SerializeField] private Transform cardShadow;
    [SerializeField] private float flipDuration = 0.5f;

    private Transform previousSide; // the side of the card that is currently shown
    private Transform targetSide; // the side of the card that is currently hidden, that will be shown

    private bool flip = false;
    private 
    float elapsedTime = 0;

    private void OnMouseEnter()
    {
        if(!flipOnMouseHover) return;
        FlipCard();
    }

    private void OnMouseExit()
    {
        if(!flipOnMouseHover) return;
        FlipCard();
    }

    public void FlipCard()
    {
        //we first check which side of the card is currently shown
        if(cardBack.activeInHierarchy) 
        {
            previousSide = cardBack.transform;
            targetSide = cardFront.transform;
        }
        else
        {
            previousSide = cardFront.transform;
            targetSide = cardBack.transform;
        }
        flip = true;
    }

    private void Update()
    {
        if(flip)
        {
            //first half of the flip: we lerp the current side x scale to 0
            if(previousSide.gameObject.activeInHierarchy) 
            {
                elapsedTime += Time.deltaTime;
                float t = elapsedTime / flipDuration;
                t = t * t * (3f - 2f * t); // Smoothstep interpolation
                previousSide.localScale = new Vector3(Mathf.Lerp(1, 0, t), 1, 1);
                if(UseShadow) cardShadow.transform.localScale = new Vector3(Mathf.Lerp(1, 0, elapsedTime / flipDuration), 1, 1);

                if(elapsedTime >= flipDuration)
                {
                    previousSide.gameObject.SetActive(false);
                    targetSide.gameObject.SetActive(true);
                    elapsedTime = 0;
                }
            }

            //second half of the flip: we lerp the target side x scale to 1
            if(targetSide.gameObject.activeInHierarchy)
            {
                elapsedTime += Time.deltaTime;
                float t = elapsedTime / flipDuration;
                t = t * t * (3f - 2f * t); // Smoothstep interpolation
                targetSide.localScale = new Vector3(Mathf.Lerp(0, 1, t), 1, 1);
                if(UseShadow) cardShadow.transform.localScale = new Vector3(Mathf.Lerp(0, 1, elapsedTime / flipDuration), 1, 1);
            
                if(elapsedTime >= flipDuration)
                {
                    flip = false;
                    elapsedTime = 0;
                }
            }
        }
    }
}