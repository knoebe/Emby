﻿using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using MediaBrowser.Server.Implementations.HttpServer;
using ServiceStack.ServiceHost;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace MediaBrowser.Api.UserLibrary
{
    /// <summary>
    /// Class BaseItemsByNameService
    /// </summary>
    /// <typeparam name="TItemType">The type of the T item type.</typeparam>
    public abstract class BaseItemsByNameService<TItemType> : BaseRestService
        where TItemType : BaseItem
    {
        /// <summary>
        /// The _user manager
        /// </summary>
        protected readonly IUserManager UserManager;
        protected readonly ILibraryManager LibraryManager;

        /// <summary>
        /// Initializes a new instance of the <see cref="BaseItemsByNameService{TItemType}" /> class.
        /// </summary>
        /// <param name="userManager">The user manager.</param>
        protected BaseItemsByNameService(IUserManager userManager, ILibraryManager libraryManager)
        {
            UserManager = userManager;
            LibraryManager = libraryManager;
        }

        /// <summary>
        /// Gets the specified request.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>Task{ItemsResult}.</returns>
        protected async Task<ItemsResult> GetResult(GetItemsByName request)
        {
            var user = UserManager.GetUserById(request.UserId);

            var item = string.IsNullOrEmpty(request.Id) ? user.RootFolder : DtoBuilder.GetItemByClientId(request.Id, UserManager, LibraryManager, user.Id);

            IEnumerable<BaseItem> items;

            if (item.IsFolder)
            {
                var folder = (Folder)item;

                items = request.Recursive ? folder.GetRecursiveChildren(user) : folder.GetChildren(user);
            }
            else
            {
                items = new[] { item };
            }

            var ibnItemsArray = GetAllItems(request, items, user).ToArray();
            IEnumerable<Tuple<string, Func<int>>> ibnItems = ibnItemsArray;

            var result = new ItemsResult
            {
                TotalRecordCount = ibnItemsArray.Length
            };

            if (request.StartIndex.HasValue || request.PageSize.HasValue)
            {
                if (request.StartIndex.HasValue)
                {
                    ibnItems = ibnItems.Skip(request.StartIndex.Value);
                }

                if (request.PageSize.HasValue)
                {
                    ibnItems = ibnItems.Take(request.PageSize.Value);
                }

            }

            var fields = GetItemFields(request).ToList();

            var tasks = ibnItems.Select(i => GetDto(i, user, fields));

            var resultItems = await Task.WhenAll(tasks).ConfigureAwait(false);

            result.Items = resultItems.Where(i => i != null).ToArray();

            return result;
        }

        /// <summary>
        /// Gets the item fields.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>IEnumerable{ItemFields}.</returns>
        private IEnumerable<ItemFields> GetItemFields(GetItemsByName request)
        {
            var val = request.Fields;

            if (string.IsNullOrEmpty(val))
            {
                return new ItemFields[] { };
            }

            return val.Split(',').Select(v => (ItemFields)Enum.Parse(typeof(ItemFields), v, true));
        }

        /// <summary>
        /// Gets all items.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <param name="items">The items.</param>
        /// <param name="user">The user.</param>
        /// <returns>IEnumerable{Tuple{System.StringFunc{System.Int32}}}.</returns>
        protected abstract IEnumerable<Tuple<string, Func<int>>> GetAllItems(GetItemsByName request, IEnumerable<BaseItem> items, User user);

        /// <summary>
        /// Gets the entity.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <returns>Task{BaseItem}.</returns>
        protected abstract Task<TItemType> GetEntity(string name);

        /// <summary>
        /// Gets the dto.
        /// </summary>
        /// <param name="stub">The stub.</param>
        /// <param name="user">The user.</param>
        /// <param name="fields">The fields.</param>
        /// <returns>Task{DtoBaseItem}.</returns>
        private async Task<BaseItemDto> GetDto(Tuple<string, Func<int>> stub, User user, List<ItemFields> fields)
        {
            BaseItem item;

            try
            {
                item = await GetEntity(stub.Item1).ConfigureAwait(false);
            }
            catch (IOException ex)
            {
                Logger.ErrorException("Error getting IBN item {0}", ex, stub.Item1);
                return null;
            }

            var dto = await new DtoBuilder(Logger).GetDtoBaseItem(item, user, fields, LibraryManager).ConfigureAwait(false);

            dto.ChildCount = stub.Item2();

            return dto;
        }
    }

    /// <summary>
    /// Class GetItemsByName
    /// </summary>
    public class GetItemsByName : IReturn<ItemsResult>
    {
        /// <summary>
        /// Gets or sets the user id.
        /// </summary>
        /// <value>The user id.</value>
        public Guid UserId { get; set; }
        /// <summary>
        /// Gets or sets the start index.
        /// </summary>
        /// <value>The start index.</value>
        public int? StartIndex { get; set; }
        /// <summary>
        /// Gets or sets the size of the page.
        /// </summary>
        /// <value>The size of the page.</value>
        public int? PageSize { get; set; }
        /// <summary>
        /// Gets or sets a value indicating whether this <see cref="GetItemsByName" /> is recursive.
        /// </summary>
        /// <value><c>true</c> if recursive; otherwise, <c>false</c>.</value>
        public bool Recursive { get; set; }
        /// <summary>
        /// Gets or sets the sort order.
        /// </summary>
        /// <value>The sort order.</value>
        public SortOrder? SortOrder { get; set; }
        /// <summary>
        /// If specified the search will be localized within a specific item or folder
        /// </summary>
        /// <value>The item id.</value>
        public string Id { get; set; }
        /// <summary>
        /// Fields to return within the items, in addition to basic information
        /// </summary>
        /// <value>The fields.</value>
        public string Fields { get; set; }
    }
}
