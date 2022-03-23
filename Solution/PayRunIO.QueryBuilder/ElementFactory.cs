namespace PayRunIO.QueryBuilder
{
    using System;
    using System.Collections.ObjectModel;
    using System.Linq;
    using System.Windows.Controls;

    using PayRunIO.Core.Enums;
    using PayRunIO.Models.Reporting;
    using PayRunIO.Models.Reporting.Conditions;
    using PayRunIO.Models.Reporting.Filtering;
    using PayRunIO.Models.Reporting.Outputs;
    using PayRunIO.Models.Reporting.Outputs.Aggregate;
    using PayRunIO.Models.Reporting.Outputs.Singular;
    using PayRunIO.Models.Reporting.Sorting;
    using PayRunIO.QueryBuilder.ViewModels;

    public class ElementFactory
    {
        public static void CreateCondition(object sender, object selectedItem)
        {
            if (selectedItem is ConditionCollectionViewModel collectionViewModel)
            {
                CreateCondition(sender, collectionViewModel.Parent);
                return;
            }

            if (!(selectedItem is GroupViewModel entityGroupViewModel))
            {
                return;
            }

            var button = (Button)sender;

            var type = (string)button.CommandParameter;

            CompareConditionBase entity;

            switch (type)
            {
                case nameof(When):
                    entity = When.New("ValueA", "ValueB");
                    break;
                case nameof(WhenNot):
                    entity = WhenNot.New("ValueA", "ValueB");
                    break;
                case nameof(WhenGreaterThan):
                    entity = WhenGreaterThan.New("ValueA", "ValueB");
                    break;
                case nameof(WhenGreaterThanEqualTo):
                    entity = WhenGreaterThanEqualTo.New("ValueA", "ValueB");
                    break;
                case nameof(WhenLessThan):
                    entity = WhenLessThan.New("ValueA", "ValueB");
                    break;
                case nameof(WhenLessThanEqualTo):
                    entity = WhenLessThanEqualTo.New("ValueA", "ValueB");
                    break;
                case nameof(WhenEqualTo):
                    entity = WhenEqualTo.New("ValueA", "ValueB");
                    break;
                case nameof(WhenNotEqualTo):
                    entity = WhenNotEqualTo.New("ValueA", "ValueB");
                    break;
                case nameof(WhenContains):
                    entity = WhenContains.New("ValueA", "ValueB");
                    break;
                case nameof(WhenNotContains):
                    entity = WhenNotContains.New("ValueA", "ValueB");
                    break;
                case nameof(WhenWithinArray):
                    entity = WhenWithinArray.New("Item1,Item2", "ValueB");
                    break;
                case nameof(WhenNotWithinArray):
                    entity = WhenNotWithinArray.New("Item1,Item2", "ValueB");
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            entityGroupViewModel.Element.Conditions.Add(entity);
            var viewModelCollection = entityGroupViewModel.Children.OfType<ConditionCollectionViewModel>().First();

            var viewModel = new ConditionViewModel(entity, viewModelCollection);
            viewModelCollection.Children.Add(viewModel);

            entityGroupViewModel.IsExpanded = true;
            viewModelCollection.IsExpanded = true;
            viewModel.IsSelected = true;
            viewModel.IsExpanded = true;
        }

        public static void CreateNewEntityGroup(object selectedItem)
        {
            if (selectedItem is GroupCollectionViewModel groupCollectionViewModel)
            {
                CreateNewEntityGroup(groupCollectionViewModel.Parent);
                return;
            }

            Collection<EntityGroup> collectionTarget;
            IHaveChildren parentViewModel;

            if (selectedItem is GroupViewModel entityGroupViewModel)
            {
                entityGroupViewModel.IsExpanded = true;
                collectionTarget = entityGroupViewModel.Element.Groups;
                parentViewModel = entityGroupViewModel.Children.OfType<GroupCollectionViewModel>().First();
            }
            else if (selectedItem is QueryViewModel queryViewModel)
            {
                queryViewModel.IsExpanded = true;
                collectionTarget = queryViewModel.Element.Groups;
                parentViewModel = queryViewModel.Children.OfType<GroupCollectionViewModel>().First();
            }
            else
            {
                return;
            }

            // Setup query entities
            var entityGroup = EntityGroup.New("GroupName");
            collectionTarget.Add(entityGroup);

            // Setup view models
            var viewModel = new GroupViewModel(entityGroup, (SelectableBase)parentViewModel);
            parentViewModel.Children.Add(viewModel);

            viewModel.Parent.IsSelected = false;
            viewModel.Parent.IsExpanded = true;
            viewModel.IsSelected = true;
            viewModel.IsExpanded = true;
        }

        public static void CreateFilter(object sender, object selectedItem)
        {
            if (selectedItem is FilterCollectionViewModel filterCollectionViewModel)
            {
                CreateFilter(sender, filterCollectionViewModel.Parent);
                return;
            }

            var button = (Button)sender;

            var type = (string)button.CommandParameter;

            FilterBase entity;

            switch (type)
            {
                case nameof(ActiveOn):
                    entity = ActiveOn.New("0001-01-01");
                    break;
                case nameof(ActiveWithin):
                    entity = ActiveWithin.New("0001-01-01,0001-01-01");
                    break;
                case nameof(OfType):
                    entity = OfType.New("TypeName");
                    break;
                case nameof(NotOfType):
                    entity = NotOfType.New("TypeName");
                    break;
                case nameof(Contain):
                    entity = Contain.New("Property", "Value");
                    break;
                case nameof(NotContain):
                    entity = NotContain.New("Property", "Value");
                    break;
                case nameof(EqualTo):
                    entity = EqualTo.New("Property", "Value");
                    break;
                case nameof(NotEqualTo):
                    entity = NotEqualTo.New("Property", "Value");
                    break;
                case nameof(GreaterThan):
                    entity = GreaterThan.New("Property", "Value");
                    break;
                case nameof(LessThan):
                    entity = LessThan.New("Property", "Value");
                    break;
                case nameof(GreaterThanEqualTo):
                    entity = GreaterThanEqualTo.New("Property", "Value");
                    break;
                case nameof(LessThanEqualTo):
                    entity = LessThanEqualTo.New("Property", "Value");
                    break;
                case nameof(Between):
                    entity = Between.New("Property", "0001-01-01", "0001-01-01");
                    break;
                case nameof(WithinArray):
                    entity = WithinArray.New("Property", "ValueA,ValueB,ValueC");
                    break;
                case nameof(NotWithinArray):
                    entity = NotWithinArray.New("Property", "ValueA,ValueB,ValueC");
                    break;
                case nameof(IsNull):
                    entity = IsNull.New("Property");
                    break;
                case nameof(IsNotNull):
                    entity = IsNotNull.New("Property");
                    break;
                case nameof(IsNullOrGreaterThan):
                    entity = IsNullOrGreaterThan.New("Property", "Value");
                    break;
                case nameof(IsNullOrGreaterThanEqualTo):
                    entity = IsNullOrGreaterThanEqualTo.New("Property", "Value");
                    break;
                case nameof(IsNullOrLessThan):
                    entity = IsNullOrLessThan.New("Property", "Value");
                    break;
                case nameof(IsNullOrLessThanEqualTo):
                    entity = IsNullOrLessThanEqualTo.New("Property", "Value");
                    break;
                case nameof(StartsWith):
                    entity = StartsWith.New("Property", "Value");
                    break;
                case nameof(EndsWith):
                    entity = EndsWith.New("Property", "Value");
                    break;
                case nameof(TakeFirst):
                    entity = TakeFirst.New(1);
                    break;
                case nameof(OfDerivedType):
                    entity = OfDerivedType.New("TypeName");
                    break;
                case nameof(NotOfDerivedType):
                    entity = NotOfDerivedType.New("TypeName");
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            FilterViewModel viewModel;
            FilterCollectionViewModel viewModelCollection;

            if (selectedItem is GroupViewModel entityGroupViewModel)
            {
                entityGroupViewModel.IsExpanded = true;

                entityGroupViewModel.Element.Filters.Add(entity);
                viewModelCollection = entityGroupViewModel.Children.OfType<FilterCollectionViewModel>().First();

                viewModel = new FilterViewModel(entity, viewModelCollection);
                viewModelCollection.Children.Add(viewModel);
            }
            else if (selectedItem is OutputViewModel outputViewModel)
            {
                if (!(outputViewModel.Element is AggregateOutputBase elementAsAggregate))
                {
                    return;
                }

                outputViewModel.IsExpanded = true;

                elementAsAggregate.Filters.Add(entity);
                viewModelCollection = outputViewModel.Children.OfType<FilterCollectionViewModel>().First();

                viewModel = new FilterViewModel(entity, viewModelCollection);
                viewModelCollection.Children.Add(viewModel);
            }
            else
            {
                return;
            }

            viewModelCollection.IsExpanded = true;
            viewModel.IsSelected = true;
            viewModel.IsExpanded = true;
        }

        public static void CreateOutput(object sender, object selectedItem)
        {
            if (selectedItem is OutputCollectionViewModel outputCollectionViewModel)
            {
                CreateOutput(sender, outputCollectionViewModel.Parent);
                return;
            }

            if (!(selectedItem is GroupViewModel entityGroupViewModel))
            {
                return;
            }

            var button = (Button)sender;

            var type = (string)button.CommandParameter;

            OutputBase entity;

            switch (type)
            {
                case nameof(RenderArrayHint):
                    entity = RenderArrayHint.New();
                    break;
                case nameof(RenderNextDate):
                    entity = RenderNextDate.New("Name", "0001-01-01", "Weekly");
                    break;
                case nameof(RenderIndex):
                    entity = RenderIndex.New();
                    break;
                case nameof(RenderEntity):
                    entity = RenderEntity.New();
                    break;
                case nameof(RenderLink):
                    entity = RenderLink.New();
                    break;
                case nameof(RenderProperty):
                    entity = RenderProperty.New("Display Name", "Property Name");
                    break;
                case nameof(RenderValue):
                    entity = RenderValue.New("Name", "Value");
                    break;
                case nameof(RenderTypeName):
                    entity = RenderTypeName.New("Name");
                    break;
                case nameof(RenderTaxPeriodDate):
                    entity = RenderTaxPeriodDate.New("Name", "2020", "1");
                    break;
                case nameof(RenderConstant):
                    entity = RenderConstant.New("Name", "Constant Name", typeof(DateTime));
                    break;
                case nameof(RenderTaxPeriod):
                    entity = RenderTaxPeriod.New("Name", "Monthly", "0001-01-01");
                    break;
                case nameof(RenderDateAdd):
                    entity = RenderDateAdd.New("Name", "0001-01-01", "Day", "1");
                    break;
                case nameof(RenderUniqueKeyFromLink):
                    entity = RenderUniqueKeyFromLink.New("Name", "[Link]");
                    break;
                case nameof(Avg):
                    entity = Avg.New("Name", "Property");
                    break;
                case nameof(Max):
                    entity = Max.New("Name", "Property");
                    break;
                case nameof(Min):
                    entity = Min.New("Name", "Property");
                    break;
                case nameof(Sum):
                    entity = Sum.New("Name", "Property");
                    break;
                case nameof(Count):
                    entity = Count.New("Name");
                    break;
                case nameof(ExpressionCalculator):
                    entity = ExpressionCalculator.New("Name", "1 + 2 - 3 * 4 / 5", rounding: RoundingOption.NotSet);
                    break;
                case nameof(Distinct):
                    entity = Distinct.New("Name", "Property");
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            entityGroupViewModel.Element.Outputs.Add(entity);
            var viewModelCollection = entityGroupViewModel.Children.OfType<OutputCollectionViewModel>().First();

            var viewModel = new OutputViewModel(entity, viewModelCollection);

            viewModelCollection.Children.Add(viewModel);

            entityGroupViewModel.IsExpanded = true;
            viewModelCollection.IsExpanded = true;
            viewModel.IsSelected = true;
            viewModel.IsExpanded = true;
        }

        public static void CreateOrdering(object sender, object selectedItem)
        {
            if (selectedItem is OrderingCollectionViewModel orderingCollectionViewModel)
            {
                CreateOrdering(sender, orderingCollectionViewModel.Parent);
                return;
            }

            if (!(selectedItem is GroupViewModel entityGroupViewModel))
            {
                return;
            }

            var button = (Button)sender;

            var type = (string)button.CommandParameter;

            OrderByBase entity;

            switch (type)
            {
                case nameof(Ascending):
                    entity = Ascending.New("Property");
                    break;
                case nameof(Descending):
                    entity = Descending.New("Property");
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            entityGroupViewModel.Element.Ordering.Add(entity);
            var viewModelCollection = entityGroupViewModel.Children.OfType<OrderingCollectionViewModel>().First();

            var viewModel = new OrderingViewModel(entity, viewModelCollection);
            viewModelCollection.Children.Add(viewModel);

            entityGroupViewModel.IsExpanded = true;
            viewModelCollection.IsExpanded = true;
            viewModel.IsSelected = true;
            viewModel.IsExpanded = true;
        }
    }
}