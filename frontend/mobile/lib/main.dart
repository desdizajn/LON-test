import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import 'screens/home_screen.dart';
import 'screens/receive_screen.dart';
import 'screens/pick_screen.dart';
import 'screens/issue_screen.dart';
import 'screens/fg_receipt_screen.dart';
import 'providers/sync_provider.dart';
import 'providers/inventory_provider.dart';

void main() {
  runApp(const LONMobileApp());
}

class LONMobileApp extends StatelessWidget {
  const LONMobileApp({super.key});

  @override
  Widget build(BuildContext context) {
    return MultiProvider(
      providers: [
        ChangeNotifierProvider(create: (_) => SyncProvider()),
        ChangeNotifierProvider(create: (_) => InventoryProvider()),
      ],
      child: MaterialApp(
        title: 'LON Mobile',
        theme: ThemeData(
          primarySwatch: Colors.blue,
          useMaterial3: true,
        ),
        home: const HomeScreen(),
        routes: {
          '/receive': (context) => const ReceiveScreen(),
          '/pick': (context) => const PickScreen(),
          '/issue': (context) => const IssueScreen(),
          '/fg-receipt': (context) => const FGReceiptScreen(),
        },
      ),
    );
  }
}
