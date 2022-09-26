using System;
using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

[RequireComponent(typeof(PhotonView))]
public class Cell : MonoBehaviourPunCallbacks, IPunInstantiateMagicCallback {
    public int row;
    public int column;
    private Stack<Piece> content = new Stack<Piece>();
    public PhotonView photonView;
    public Material baseMaterial;

    public void Start() {
        photonView = gameObject.GetComponent<PhotonView>();
        baseMaterial = GetComponent<MeshRenderer>().material;
    }
    public void Reset() {
        content.Clear();
        GetComponent<MeshRenderer>().material = baseMaterial;
    }

   
    void OnMouseOver(){
        if(!Input.GetMouseButtonDown(0)) return;
        GameManager.Instance.AttemptToPlaceSelectedPieceInCell(this);
    }

    public void PlacePiece(Piece selectedPiece) {
        //half the cell size, well add the heigt of each piece in the stack
        float resultingHeight = 0f;
        foreach (var pieceInContent in content) {
            resultingHeight += pieceInContent.model.GetComponent<MeshCollider>().bounds.size.y;
        }
        selectedPiece.transform.localPosition = new Vector3(transform.position.x,resultingHeight,transform.position.z);
        GameManager.Instance.ClearSelectedPiece();
        photonView.RPC("PushPiece", RpcTarget.Others, selectedPiece.photonView.ViewID);
        PushPiece(selectedPiece.photonView.ViewID);
        

    }

    [PunRPC]
    private void PushPiece(int id) {
        PhotonView piecePhotonView = PhotonView.Find(id);
        if (piecePhotonView == null) {
            Debug.Log("PhotonView of piece not found");
            return;
        }

        Piece selectedPiece = piecePhotonView.gameObject.GetComponent<Piece>();
        if (selectedPiece == null) {
            Debug.Log("PhotonView had no Piece");
            return;
        }
        content.Push(selectedPiece);
        selectedPiece.containingCell = this;
        
        int? winner = GameManager.Instance.CheckForWinner();
        if (winner != null) {
            StartCoroutine(GameManager.Instance.EndMatch((int)winner));
        }
        else {
            GameManager.Instance.NextTurn();
        }
        
    }

    public Piece Peek() {
        if (content.TryPeek(out Piece piece)) {
            return piece;
        }
        return null;
    }

    public bool CanPieceBePushed(Piece piece) {
        Piece topPieceOfStack = Peek();
        if (piece.containingCell != null) {
            if (piece.containingCell == this) {
                Debug.Log("Piece already in this cell");
                return false;
            }
            
            if (Math.Abs(piece.containingCell.row - row) +
                         Math.Abs(piece.containingCell.column - column) != 1)  {
                Debug.Log("Attempting to move more cells than allowed");
                return false;
            }
        }
        else{
            if (Peek() != null) {
                Debug.Log("Attempting to place piece from outside board to an already occupied cell");
                return false;
            }
        }
        
        if (topPieceOfStack == null) {
            Debug.Log("Empty cell, can push");
            return true;
        }
        if (topPieceOfStack.rank < piece.rank) {
            Debug.Log("Top rank of stack is smaller than piece");
            return true;
        }
        return false;
    }
    [PunRPC]
    public void Pop() {
        Piece topPieceOfStack = Peek();
        if (topPieceOfStack != null) {
            content.Pop();
        }

    }
    public void OnPhotonInstantiate(PhotonMessageInfo info) {
        object[] instantiationData = info.photonView.InstantiationData;

        row = (int)instantiationData[0];
        column = (int)instantiationData[1];
        GameManager.Instance.boardData[row, column] = this;
    }
}

