/*
Copyright (c) 2018-2023 Festo SE & Co. KG <https://www.festo.com/net/de_de/Forms/web/contact_international>
Author: Michael Hoffmeister

This source code is licensed under the Apache License 2.0 (see LICENSE.txt).

This source code may use other Open Source software components (see LICENSE.txt).
*/

using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AasxIntegrationBase;
using AnyUi;

namespace AasxPackageExplorer
{
    /// <summary>
    /// Creates a flyout in order to select items from a list
    /// </summary>
    public partial class ShowVerificationResultsFlyout : UserControl, IFlyoutControl
    {
        public event IFlyoutControlAction ControlClosed;

        public IEnumerable VerificationItems = null;

        public bool ContinueSelected = false;

        public bool DoHint, DoWarning, DoSpecViolation, DoSchemaViolation;

        public ShowVerificationResultsFlyout()
        {
            InitializeComponent();
        }

        //
        // Outer
        //

        public void ControlStart()
        {
        }

        public void ControlPreviewKeyDown(KeyEventArgs e)
        {
        }

        public void LambdaActionAvailable(AnyUiLambdaActionBase la)
        {
        }

        private void DataGridTable_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }

        //
        // Mechanics
        //

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            // fill table
            DataGridTable.ItemsSource = VerificationItems;

        }

        private void ButtonSelect_Click(object sender, RoutedEventArgs e)
        {
            this.ContinueSelected = true;
            ControlClosed?.Invoke();
        }

        private void ButtonClose_Click(object sender, RoutedEventArgs e)
        {
            this.ContinueSelected = false;
            ControlClosed?.Invoke();
        }

    }
}
