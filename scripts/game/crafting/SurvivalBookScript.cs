using Godot;

public partial class SurvivalBookScript : Control, IItemOpenableUI
{
    [Export] public CraftingBookResource Book { get; set; }

    private RichTextLabel _itemName;
    private RichTextLabel _itemIngredientsShortDesc;
    private TextureRect[] _requiredIngredientItems;
    private TextureRect _recievedItemTextureRect;
    private Button _craftButton;
    private RichTextLabel _itemDescLong;
    private TextureRect _itemGraphicTextureRect;

    private playerHUD _playerHUD;

    private int _currentPageIndex = 0;
    private bool _isOpen = false;

    public override void _Ready()
    {
        _itemName = GetNode<RichTextLabel>("%ItemName");
        _itemIngredientsShortDesc = GetNode<RichTextLabel>("%ItemIngredientsShortDesc");

        _requiredIngredientItems = new TextureRect[9];

        for (int i = 0; i < 9; i++)
            _requiredIngredientItems[i] = GetNode<TextureRect>($"%ItemTextureRect{i + 1}");

        _recievedItemTextureRect = GetNode<TextureRect>("%RecievedItemTextureRect");
        _craftButton = GetNode<Button>("%CraftButton");
        _itemDescLong = GetNode<RichTextLabel>("%ItemDescLong");
        _itemGraphicTextureRect = GetNode<TextureRect>("%ItemGraphicTextureRect");

        Visible = false;
        SetProcess(false);
    }

    public void OpenFromItem(Node player, ItemInstance item)
    {
        _playerHUD = GetTree().GetFirstNodeInGroup("hud") as playerHUD;

        if (_playerHUD == null)
            GD.PushError("SurvivalBookScript: playerHUD not found in group 'hud'.");

        OpenBook();
    }

    public void OpenBook()
    {        
        _isOpen = true;
        Visible = true;
        SetProcess(true);
        MoveToFront();
        if (robinsonGlobals.Instance != null)
        {
            robinsonGlobals.Instance.OpenInventory = true;
        }

        ShowPage(_currentPageIndex);
        ShowBookPrompts();

        Input.MouseMode = Input.MouseModeEnum.Visible;
    }

    public void CloseBook()
    {
        _isOpen = false;
        Visible = false;
        SetProcess(false);

        HideBookPrompts();

        if (robinsonGlobals.Instance != null)
        {
            robinsonGlobals.Instance.OpenItemUI = false;
            robinsonGlobals.Instance.CanMove = true;
            robinsonGlobals.Instance.OpenInventory = false;
        }

        Input.MouseMode = Input.MouseModeEnum.Captured;

        QueueFree();
    }

    public override void _Process(double delta)
    {
        if (!_isOpen)
            return;

        if (Input.IsActionJustPressed("game_survival_book_next_page"))
            NextPage();

        if (Input.IsActionJustPressed("game_survival_book_previous_page"))
            PreviousPage();

        if (Input.IsActionJustPressed("game_survival_book_close"))
            CloseBook();
    }

    private void ShowBookPrompts()
    {
        if (_playerHUD == null)
            return;

        _playerHUD.ShowButtonAction(
            "survival_book_previous",
            "game_survival_book_previous_page",
            "Previous Page",
            false
        );

        _playerHUD.ShowButtonAction(
            "survival_book_next",
            "game_survival_book_next_page",
            "Next Page",
            false
        );

        _playerHUD.ShowButtonAction(
            "survival_book_close",
            "game_survival_book_close",
            "Close",
            false
        );
    }

    private void HideBookPrompts()
    {
        if (_playerHUD == null)
            return;

        _playerHUD.HideButtonAction("survival_book_previous");
        _playerHUD.HideButtonAction("survival_book_next");
        _playerHUD.HideButtonAction("survival_book_close");
    }

    public void NextPage()
    {
        if (Book == null || Book.Pages == null || Book.Pages.Count == 0)
            return;

        _currentPageIndex++;

        if (_currentPageIndex >= Book.Pages.Count)
            _currentPageIndex = 0;

        ShowPage(_currentPageIndex);
    }

    public void PreviousPage()
    {
        if (Book == null || Book.Pages == null || Book.Pages.Count == 0)
            return;

        _currentPageIndex--;

        if (_currentPageIndex < 0)
            _currentPageIndex = Book.Pages.Count - 1;

        ShowPage(_currentPageIndex);
    }

    public void ShowPage(int pageIndex)
    {
        if (Book == null || Book.Pages == null || Book.Pages.Count == 0)
        {
            ClearPage();
            return;
        }

        if (pageIndex < 0 || pageIndex >= Book.Pages.Count)
            return;

        _currentPageIndex = pageIndex;

        CraftingBookPageResource page = Book.Pages[pageIndex];

        if (page == null)
        {
            ClearPage();
            return;
        }

        _itemName.Text = page.DisplayName;
        _itemIngredientsShortDesc.Text = page.ShortDescription;
        _itemDescLong.Text = page.Description;
        _itemGraphicTextureRect.Texture = page.Graphic;

        ShowRecipe(page.Recipe);
    }

    private void ShowRecipe(CraftingRecipeResource recipe)
    {
        for (int i = 0; i < _requiredIngredientItems.Length; i++)
        {
            TextureRect slot = _requiredIngredientItems[i];

            if (recipe != null && recipe.Ingredients != null && i < recipe.Ingredients.Count && recipe.Ingredients[i] != null)
            {
                slot.Texture = recipe.Ingredients[i].Icon;
                slot.Visible = true;
            }
            else
            {
                slot.Texture = null;
                slot.Visible = false;
            }
        }

        if (recipe != null && recipe.ResultItem != null)
        {
            _recievedItemTextureRect.Texture = recipe.ResultItem.Icon;
            _recievedItemTextureRect.Visible = true;
        }
        else
        {
            _recievedItemTextureRect.Texture = null;
            _recievedItemTextureRect.Visible = false;
        }
    }

    private void ClearPage()
    {
        _itemName.Text = "";
        _itemIngredientsShortDesc.Text = "";
        _itemDescLong.Text = "";

        _itemGraphicTextureRect.Texture = null;
        _recievedItemTextureRect.Texture = null;

        foreach (TextureRect slot in _requiredIngredientItems)
        {
            slot.Texture = null;
            slot.Visible = false;
        }
    }
}