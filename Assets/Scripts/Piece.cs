using Photon.Pun;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
public class Piece : MonoBehaviourPunCallbacks, IPunInstantiateMagicCallback {
    public int owner;
    public int rank;
    public GameObject model;
    public Cell containingCell = null;

    private Material selectedMaterial;
    private Material whiteTeam;
    private Material orangeTeam;

    public Material baseMaterial;
    public PhotonView photonView;

    // Start is called before the first frame update
    void Start()
    {
        photonView = gameObject.GetComponent<PhotonView>();
        PieceModel pieceModel = model.GetComponent<PieceModel>();
        pieceModel.piece = this;
    }

 

    public void MarkAsSelected() {
        model.GetComponent<MeshRenderer>().material = selectedMaterial;
    }
    
    public void UnmarkAsSelected() {
        model.GetComponent<MeshRenderer>().material = baseMaterial;
    }

    public bool CanPieceBeMoved() {
        if (GameManager.Instance.owner != owner) {
            Debug.Log(GameManager.Instance.owner+" tried to move a piece owned by "+ owner);
            return false;
        }
        if (containingCell == null || containingCell.Peek() == this) {
            return true;
        }
        return false;
    }
    
    public void OnPhotonInstantiate(PhotonMessageInfo info) {
        selectedMaterial = Resources.Load<Material>("Materials/Blue");
        whiteTeam = Resources.Load<Material>("Materials/White");
        orangeTeam = Resources.Load<Material>("Materials/Orange");
        
        object[] instantiationData = info.photonView.InstantiationData;

        owner = (int)instantiationData[0];
        if (owner == 1) {
            baseMaterial = whiteTeam;
        }
        else if (owner == 2) {
            baseMaterial = orangeTeam;
        }
        model.GetComponent<MeshRenderer>().material = baseMaterial;
    }
    
}
