   M S B A M L   `   `   `  ����   ACPLogAnalyzer    ACPLogAnalyzer.MainWindow   W SPresentationCore, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35R NWindowsBase, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35\ XPresentationFramework, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35U QWPFToolkit, Version=3.5.40128.1, Culture=neutral, PublicKeyToken=31bf3856ad364e35F 9http://schemas.microsoft.com/winfx/2006/xaml/presentation     R NSystem.Xaml, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089:x,http://schemas.microsoft.com/winfx/2006/xaml     -     � Title$  ACP Log Analyzer��$	��671��$
��1031�� � 
ResizeMode$ CanMinimize=� � WindowStartupLocation$ CenterScreen=�.��� -   $��MainGrid��.+��� $-��'Report (double-click to view in editor)��$	��556��$
��Left=�$��	610,5,0,0q�$	��Top=�$	��399��.��� .+��� -   $��Save...��$��25��$
��Left=�$��245,500,0,0q�$	��Top=�$��58�� � ToolTip$% Save the current report to disk���� -   $��ListBoxReport��$	��488��$
��Left=�$��6,6,0,0q�$	��Top=�$	��375��
�������$ Report contents���� -   $��ButtonConfigReport��$��Configure...��$��25��$
��Left=�$��309,500,0,0q�$	��Top=�$��72��$0 *Configure which items appear in the report���� -   $��ButtonReportAll��$
��Left=�$!��Generate Report On All Logs��$	��233��$��	6,500,0,0q�$��25��$	��Top=�$3 -Create a report on all log files listed below���� $��53��$
��Left=�$��	69,14,0,0q�$��ACP Log Analyzer��$	��Top=�$	��253�� �� TextAlignment$
 Left=�$��32(�$��Normal$�$��
ExtraLight"��� $��50��$
��Left=�$��Uniform=�$	��Top=�$��50��$2��,/ACPLogAnalyzer;component/Images/ACPLASm.png��$��8,11,0,0q��� -   $��	GroupBox2��$��Graphs��$��62��$
��Left=�$��610,567,0,0q�$	��Top=�$	��399��.��� .+��� -   $��
ButtonPlot��$��Plot...��$��25��$
��Left=�$��	245,6,0,0q�$	��Top=�$��58���� -	   $��ButtonConfigPlot��$��Configure...��$��25��$
��Left=�$��	309,6,0,0q�$	��Top=�$��72���� -
   $��ComboBoxGraphs��$��25��$
��Left=�$��6,6,0,0q�$	��Top=�$	��233������  �� Path$ _graphOptions��� -   $��GroupBoxLogsList��$.��(Logs (0) (drag log files into this list)��$	��171��$
��Left=�$��	8,165,0,0q�$	��Top=�$	��594��.��� .+��� -   $��ListBoxLogs��$	��137��$
��Left=�$��6,6,0,0q�$	��Top=�$	��570��
������� J� 	AllowDrop . $ ACP log list�� � ContextMenu �� -   $��ContextMenuLogs��.@�m� -   $��Remove log from list��m� $��Cancel���� -   $��	GroupBox4��$3��-Log contents (double-click to view in editor)��$	��287��$
��Left=�$��	8,342,0,0q�$	��Top=�$	��594��.��� .+��� -   $��
ListBoxLog��$	��252��$
��Left=�$��6,6,0,0q�$	��Top=�$	��570��
�������$ ACP log viewer�� �� SelectionMode$ Single=��� -   $��	GroupBox3��$��	Find logs��$��94��$
��Left=�$��8,65,0,0q�$	��Top=�$	��594��.��� .+��� -   $��ButtonReadLogs��$��Add All Logs In Path��$��25��$
��Left=�$��
391,39,0,0q�$	��Top=�$	��185��$W QSearch the directory (and sub-directories) in the selected path for ACP log files���� -   $��
ButtonPath��$��Select Path...��$��23��$��Right=�$��0,9,6,0q�$	��Top=�$��98��$/ )Select a path to search for ACP log files���� -   $��	ButtonLog��{�. $��Add Single Log ...��$��25��$
��Left=�$��
199,39,0,0q�$	��Top=�$	��185��$, &Browse to select a single ACP log file���� -   $��ComboBoxPath��$��23��$
��Left=�$��6,9,0,0q�$	��Top=�$	��463�� .  	 �� 
IsEditable	 .  $* $Select a path to search for ACP logs���� -   $��ButtonClearLogList��$
��Left=�$��Clear Log List��$��25��$��6,39,0,0q�$	��185��$	��Top=�$! Clear the list of log files���� -   $��	GroupBox5��$��Application��$��63��$
��Left=�$��	322,4,0,0q�$	��Top=�$	��280��.��� .+��� -   $��ButtonHelpBrowser��$
��Help��$��25��$
��Left=�$��10,6,0,0q�$	��Top=�$��80���� -   $��ButtonUpdate��$��Update��$��25��$
��Left=�$��	182,6,0,0q�$	��Top=�$��80���� -   $��
ButtonHelp��$��About��$��25��$
��Left=�$��96,6,0,0q�$	��Top=�$��80��