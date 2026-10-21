using Library_Project.Model;
using Library_Project.Services;
using Library_Project.Services.Interfaces;
using Moq;

namespace Library_Project_Tests
{
    /// <summary>
    /// Юніт-тести бізнес-логіки сервісу <see cref="LibraryService"/>.
    /// Залежності сервісу (<see cref="IBookRepository"/>, <see cref="IMemberService"/>,
    /// <see cref="INotificationService"/>) замінено mock-об'єктами Moq.
    /// Кожен тест побудовано за шаблоном Arrange – Act – Assert.
    /// </summary>
    public class LibraryServiceTests
    {
        private const string BookTitle = "Clean Code";
        private const string OtherBookTitle = "Refactoring";
        private const string UnknownBookTitle = "Unknown Book";
        private const int ValidMemberId = 1;
        private const int InvalidMemberId = -1;

        private readonly Mock<IBookRepository> _repoMock;
        private readonly Mock<IMemberService> _memberMock;
        private readonly Mock<INotificationService> _notifMock;
        private readonly LibraryService _service;

        public LibraryServiceTests()
        {
            _repoMock = new Mock<IBookRepository>();
            _memberMock = new Mock<IMemberService>();
            _notifMock = new Mock<INotificationService>();

            _service = new LibraryService(_repoMock.Object, _memberMock.Object, _notifMock.Object);
        }

        #region Helper methods

        /// <summary>
        /// Створює об'єкт книги з указаною назвою та кількістю примірників.
        /// </summary>
        private static Book CreateBook(string title, int copies)
        {
            return new Book { Title = title, Copies = copies };
        }

        /// <summary>
        /// Налаштовує mock репозиторію так, щоб пошук за назвою книги повертав переданий об'єкт.
        /// </summary>
        private void SetupFindBook(string title, Book? book)
        {
            _repoMock.Setup(r => r.FindBook(title)).Returns(book!);
        }

        /// <summary>
        /// Налаштовує mock сервісу читачів: читач із указаним ідентифікатором валідний чи ні.
        /// </summary>
        private void SetupMember(int memberId, bool isValid)
        {
            _memberMock.Setup(m => m.IsValidMember(memberId)).Returns(isValid);
        }

        #endregion

        #region AddBook

        /// <summary>
        /// [Theory] / [InlineData] + Assert.Throws:
        /// AddBook має кидати ArgumentException, якщо назва книги null, порожня або складається з пробілів.
        /// </summary>
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void AddBook_TitleIsNullOrWhiteSpace_ThrowsArgumentException(string? invalidTitle)
        {
            // Arrange
            const int copies = 1;

            // Act
            Action act = () => _service.AddBook(invalidTitle!, copies);

            // Assert
            Assert.Throws<ArgumentException>(act);
        }

        /// <summary>
        /// [Theory] / [InlineData] + Assert.Throws:
        /// AddBook має кидати ArgumentException, якщо кількість примірників нульова або від'ємна.
        /// </summary>
        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(-100)]
        public void AddBook_CopiesNotPositive_ThrowsArgumentException(int invalidCopies)
        {
            // Act
            Action act = () => _service.AddBook(BookTitle, invalidCopies);

            // Assert
            Assert.Throws<ArgumentException>(act);
        }

        /// <summary>
        /// Verify(..., Times.Never) + It.IsAny:
        /// якщо валідація вхідних даних не пройшла, книга не повинна зберігатися в репозиторії.
        /// </summary>
        [Fact]
        public void AddBook_InvalidCopies_NeverSavesBook()
        {
            // Arrange
            const int invalidCopies = 0;

            // Act
            Record.Exception(() => _service.AddBook(BookTitle, invalidCopies));

            // Assert
            _repoMock.Verify(r => r.SaveBook(It.IsAny<Book>()), Times.Never);
        }

        /// <summary>
        /// Verify() + It.Is(predicate):
        /// для нової назви репозиторій має отримати новий об'єкт Book із тією ж назвою та кількістю примірників.
        /// </summary>
        [Fact]
        public void AddBook_NewTitle_SavesNewBookWithGivenTitleAndCopies()
        {
            // Arrange
            const int copies = 3;
            SetupFindBook(BookTitle, null);

            // Act
            _service.AddBook(BookTitle, copies);

            // Assert
            _repoMock.Verify(r => r.SaveBook(It.Is<Book>(b => b.Title == BookTitle && b.Copies == copies)), Times.Once);
        }

        /// <summary>
        /// Assert.NotNull:
        /// для нової назви сервіс має передати в репозиторій створений (не null) об'єкт книги.
        /// </summary>
        [Fact]
        public void AddBook_NewTitle_PassesCreatedBookToRepository()
        {
            // Arrange
            Book? savedBook = null;
            SetupFindBook(BookTitle, null);
            _repoMock.Setup(r => r.SaveBook(It.IsAny<Book>()))
                     .Callback<Book>(b => savedBook = b);

            // Act
            _service.AddBook(BookTitle, 2);

            // Assert
            Assert.NotNull(savedBook);
        }

        /// <summary>
        /// [Theory] / [InlineData] + Assert.Equal:
        /// якщо книга вже існує, кількість її примірників має збільшитися на додану кількість.
        /// </summary>
        [Theory]
        [InlineData(0, 1, 1)]
        [InlineData(2, 3, 5)]
        [InlineData(10, 5, 15)]
        public void AddBook_ExistingTitle_IncreasesCopies(int initialCopies, int addedCopies, int expectedCopies)
        {
            // Arrange
            var existingBook = CreateBook(BookTitle, initialCopies);
            SetupFindBook(BookTitle, existingBook);

            // Act
            _service.AddBook(BookTitle, addedCopies);

            // Assert
            Assert.Equal(expectedCopies, existingBook.Copies);
        }

        /// <summary>
        /// Verify(..., Times.Exactly(n)):
        /// два послідовні додавання тієї ж книги мають призвести рівно до двох збережень у репозиторії.
        /// </summary>
        [Fact]
        public void AddBook_SameTitleAddedTwice_SavesBookExactlyTwice()
        {
            // Arrange
            var existingBook = CreateBook(BookTitle, 1);
            SetupFindBook(BookTitle, existingBook);

            // Act
            _service.AddBook(BookTitle, 1);
            _service.AddBook(BookTitle, 1);

            // Assert
            _repoMock.Verify(r => r.SaveBook(existingBook), Times.Exactly(2));
        }

        #endregion

        #region BorrowBook

        /// <summary>
        /// Assert.Throws:
        /// BorrowBook має кидати InvalidOperationException, якщо читач невалідний.
        /// </summary>
        [Fact]
        public void BorrowBook_InvalidMember_ThrowsInvalidOperationException()
        {
            // Arrange
            SetupMember(InvalidMemberId, false);

            // Act
            Action act = () => _service.BorrowBook(InvalidMemberId, BookTitle);

            // Assert
            Assert.Throws<InvalidOperationException>(act);
        }

        /// <summary>
        /// Verify(..., Times.Never) + It.IsAny:
        /// для невалідного читача сервіс не повинен навіть шукати книгу в репозиторії.
        /// </summary>
        [Fact]
        public void BorrowBook_InvalidMember_NeverSearchesRepository()
        {
            // Arrange
            SetupMember(InvalidMemberId, false);

            // Act
            Record.Exception(() => _service.BorrowBook(InvalidMemberId, BookTitle));

            // Assert
            _repoMock.Verify(r => r.FindBook(It.IsAny<string>()), Times.Never);
        }

        /// <summary>
        /// Assert.True:
        /// валідний читач успішно бере книгу, яка є в наявності.
        /// </summary>
        [Fact]
        public void BorrowBook_ValidMemberAndBookAvailable_ReturnsTrue()
        {
            // Arrange
            SetupMember(ValidMemberId, true);
            SetupFindBook(BookTitle, CreateBook(BookTitle, 3));

            // Act
            var result = _service.BorrowBook(ValidMemberId, BookTitle);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Assert.False:
        /// якщо книги з такою назвою немає в репозиторії, BorrowBook повертає false.
        /// </summary>
        [Fact]
        public void BorrowBook_BookNotFound_ReturnsFalse()
        {
            // Arrange
            SetupMember(ValidMemberId, true);
            SetupFindBook(UnknownBookTitle, null);

            // Act
            var result = _service.BorrowBook(ValidMemberId, UnknownBookTitle);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// [Theory] / [InlineData] + Assert.False:
        /// якщо примірників книги не залишилось (0 або некоректне від'ємне значення), BorrowBook повертає false.
        /// </summary>
        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void BorrowBook_NoCopiesLeft_ReturnsFalse(int copies)
        {
            // Arrange
            SetupMember(ValidMemberId, true);
            SetupFindBook(BookTitle, CreateBook(BookTitle, copies));

            // Act
            var result = _service.BorrowBook(ValidMemberId, BookTitle);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Assert.Equal:
        /// після успішного видавання кількість примірників зменшується рівно на один.
        /// </summary>
        [Fact]
        public void BorrowBook_BookAvailable_DecreasesCopiesByOne()
        {
            // Arrange
            var book = CreateBook(BookTitle, 3);
            SetupMember(ValidMemberId, true);
            SetupFindBook(BookTitle, book);

            // Act
            _service.BorrowBook(ValidMemberId, BookTitle);

            // Assert
            Assert.Equal(2, book.Copies);
        }

        /// <summary>
        /// Assert.NotEqual:
        /// після видавання книги кількість примірників не повинна залишатися такою ж, як до операції.
        /// </summary>
        [Fact]
        public void BorrowBook_BookAvailable_ChangesCopiesCount()
        {
            // Arrange
            const int initialCopies = 1;
            var book = CreateBook(BookTitle, initialCopies);
            SetupMember(ValidMemberId, true);
            SetupFindBook(BookTitle, book);

            // Act
            _service.BorrowBook(ValidMemberId, BookTitle);

            // Assert
            Assert.NotEqual(initialCopies, book.Copies);
        }

        /// <summary>
        /// Verify():
        /// після успішного видавання читач отримує сповіщення NotifyBorrow з правильними аргументами рівно один раз.
        /// </summary>
        [Fact]
        public void BorrowBook_BookAvailable_NotifiesMemberOnce()
        {
            // Arrange
            SetupMember(ValidMemberId, true);
            SetupFindBook(BookTitle, CreateBook(BookTitle, 1));

            // Act
            _service.BorrowBook(ValidMemberId, BookTitle);

            // Assert
            _notifMock.Verify(n => n.NotifyBorrow(ValidMemberId, BookTitle), Times.Once);
        }

        /// <summary>
        /// Verify(..., Times.Never) + It.IsAny:
        /// якщо книги немає в наявності, сповіщення про видавання не надсилається.
        /// </summary>
        [Fact]
        public void BorrowBook_NoCopiesLeft_NeverSendsNotification()
        {
            // Arrange
            SetupMember(ValidMemberId, true);
            SetupFindBook(BookTitle, CreateBook(BookTitle, 0));

            // Act
            _service.BorrowBook(ValidMemberId, BookTitle);

            // Assert
            _notifMock.Verify(n => n.NotifyBorrow(It.IsAny<int>(), It.IsAny<string>()), Times.Never);
        }

        /// <summary>
        /// Verify(..., Times.AtLeastOnce):
        /// під час видавання сервіс має хоча б раз перевірити, чи є читач валідним.
        /// </summary>
        [Fact]
        public void BorrowBook_AnyRequest_ChecksMemberAtLeastOnce()
        {
            // Arrange
            SetupMember(ValidMemberId, true);
            SetupFindBook(BookTitle, CreateBook(BookTitle, 1));

            // Act
            _service.BorrowBook(ValidMemberId, BookTitle);

            // Assert
            _memberMock.Verify(m => m.IsValidMember(ValidMemberId), Times.AtLeastOnce);
        }

        /// <summary>
        /// Verify(..., Times.AtMost(n)):
        /// якщо є лише 2 примірники, а книгу намагаються взяти 3 рази,
        /// сповіщень про видавання має бути не більше ніж 2.
        /// </summary>
        [Fact]
        public void BorrowBook_MoreRequestsThanCopies_NotifiesAtMostCopiesCount()
        {
            // Arrange
            const int availableCopies = 2;
            SetupMember(ValidMemberId, true);
            SetupFindBook(BookTitle, CreateBook(BookTitle, availableCopies));

            // Act
            _service.BorrowBook(ValidMemberId, BookTitle);
            _service.BorrowBook(ValidMemberId, BookTitle);
            _service.BorrowBook(ValidMemberId, BookTitle);

            // Assert
            _notifMock.Verify(n => n.NotifyBorrow(ValidMemberId, BookTitle), Times.AtMost(availableCopies));
        }

        #endregion

        #region ReturnBook

        /// <summary>
        /// Assert.False:
        /// повернення книги, якої немає в репозиторії, завершується невдало.
        /// </summary>
        [Fact]
        public void ReturnBook_BookNotFound_ReturnsFalse()
        {
            // Arrange
            SetupFindBook(UnknownBookTitle, null);

            // Act
            var result = _service.ReturnBook(ValidMemberId, UnknownBookTitle);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Assert.True:
        /// повернення наявної в каталозі книги завершується успішно.
        /// </summary>
        [Fact]
        public void ReturnBook_BookExists_ReturnsTrue()
        {
            // Arrange
            SetupFindBook(BookTitle, CreateBook(BookTitle, 0));

            // Act
            var result = _service.ReturnBook(ValidMemberId, BookTitle);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Verify() + It.Is(predicate):
        /// після повернення книга зберігається в репозиторії зі збільшеною на один кількістю примірників.
        /// </summary>
        [Fact]
        public void ReturnBook_BookExists_SavesBookWithIncreasedCopies()
        {
            // Arrange
            SetupFindBook(BookTitle, CreateBook(BookTitle, 4));

            // Act
            _service.ReturnBook(ValidMemberId, BookTitle);

            // Assert
            _repoMock.Verify(r => r.SaveBook(It.Is<Book>(b => b.Title == BookTitle && b.Copies == 5)));
        }

        /// <summary>
        /// Verify() + It.Is(predicate):
        /// після повернення надсилається сповіщення NotifyReturn саме для цього читача й цієї книги.
        /// </summary>
        [Fact]
        public void ReturnBook_BookExists_NotifiesReturnForSameMemberAndTitle()
        {
            // Arrange
            SetupFindBook(BookTitle, CreateBook(BookTitle, 1));

            // Act
            _service.ReturnBook(ValidMemberId, BookTitle);

            // Assert
            _notifMock.Verify(n => n.NotifyReturn(
                It.Is<int>(id => id == ValidMemberId),
                It.Is<string>(t => t == BookTitle)), Times.Once);
        }

        /// <summary>
        /// Verify(..., Times.Never) + It.IsAny:
        /// якщо книгу не знайдено, вона не зберігається і сповіщення про повернення не надсилається.
        /// </summary>
        [Fact]
        public void ReturnBook_BookNotFound_NeverSendsReturnNotification()
        {
            // Arrange
            SetupFindBook(UnknownBookTitle, null);

            // Act
            _service.ReturnBook(ValidMemberId, UnknownBookTitle);

            // Assert
            _notifMock.Verify(n => n.NotifyReturn(It.IsAny<int>(), It.IsAny<string>()), Times.Never);
        }

        #endregion

        #region GetAvailableBooks

        /// <summary>
        /// Assert.Contains:
        /// результат має містити книгу, у якої є вільні примірники.
        /// </summary>
        [Fact]
        public void GetAvailableBooks_BookWithCopies_IsContainedInResult()
        {
            // Arrange
            var availableBook = CreateBook(BookTitle, 2);
            var outOfStockBook = CreateBook(OtherBookTitle, 0);
            _repoMock.Setup(r => r.GetAllBooks()).Returns(new List<Book> { availableBook, outOfStockBook });

            // Act
            var result = _service.GetAvailableBooks();

            // Assert
            Assert.Contains(availableBook, result);
        }

        /// <summary>
        /// Assert.Null:
        /// книга без вільних примірників не повинна потрапляти в список доступних.
        /// </summary>
        [Fact]
        public void GetAvailableBooks_BookWithoutCopies_IsNotInResult()
        {
            // Arrange
            var availableBook = CreateBook(BookTitle, 2);
            var outOfStockBook = CreateBook(OtherBookTitle, 0);
            _repoMock.Setup(r => r.GetAllBooks()).Returns(new List<Book> { availableBook, outOfStockBook });

            // Act
            var result = _service.GetAvailableBooks();

            // Assert
            Assert.Null(result.Find(b => b.Title == OtherBookTitle));
        }

        /// <summary>
        /// Assert.Empty:
        /// якщо всі книги видані (кількість примірників 0), список доступних книг порожній.
        /// </summary>
        [Fact]
        public void GetAvailableBooks_AllBooksOutOfStock_ReturnsEmptyList()
        {
            // Arrange
            _repoMock.Setup(r => r.GetAllBooks()).Returns(new List<Book>
            {
                CreateBook(BookTitle, 0),
                CreateBook(OtherBookTitle, 0)
            });

            // Act
            var result = _service.GetAvailableBooks();

            // Assert
            Assert.Empty(result);
        }

        /// <summary>
        /// Assert.NotEmpty:
        /// якщо хоча б одна книга має вільні примірники, список доступних книг не порожній.
        /// </summary>
        [Fact]
        public void GetAvailableBooks_SomeBooksInStock_ReturnsNotEmptyList()
        {
            // Arrange
            _repoMock.Setup(r => r.GetAllBooks()).Returns(new List<Book>
            {
                CreateBook(BookTitle, 1),
                CreateBook(OtherBookTitle, 0)
            });

            // Act
            var result = _service.GetAvailableBooks();

            // Assert
            Assert.NotEmpty(result);
        }

        /// <summary>
        /// Assert.NotNull:
        /// навіть для порожнього каталогу сервіс повертає не null, а порожню колекцію.
        /// </summary>
        [Fact]
        public void GetAvailableBooks_EmptyCatalog_ReturnsNotNull()
        {
            // Arrange
            _repoMock.Setup(r => r.GetAllBooks()).Returns(new List<Book>());

            // Act
            var result = _service.GetAvailableBooks();

            // Assert
            Assert.NotNull(result);
        }

        /// <summary>
        /// Verify(..., Times.Exactly(n)):
        /// для отримання списку доступних книг репозиторій опитується рівно один раз.
        /// </summary>
        [Fact]
        public void GetAvailableBooks_Called_QueriesRepositoryExactlyOnce()
        {
            // Arrange
            _repoMock.Setup(r => r.GetAllBooks()).Returns(new List<Book>());

            // Act
            _service.GetAvailableBooks();

            // Assert
            _repoMock.Verify(r => r.GetAllBooks(), Times.Exactly(1));
        }

        #endregion
    }
}
