using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PieceModel : MonoBehaviour {
    public Piece piece;
    // Start is called before the first frame update
    public void OnMouseOver(){
        if(Input.GetMouseButtonDown(0)) {
            Debug.Log("Clicked" + piece.rank);
            if (GameManager.Instance.whoseTurn == GameManager.Instance.owner && piece.CanPieceBeMoved()) {
                GameManager.Instance.SelectPiece(piece);
            }
            else {
                Debug.Log("This piece cant be moved");
            }
        }
    }
}
