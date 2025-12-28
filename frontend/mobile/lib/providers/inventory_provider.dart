import 'package:flutter/foundation.dart';

class InventoryProvider with ChangeNotifier {
  List<Map<String, dynamic>> _items = [];

  List<Map<String, dynamic>> get items => _items;

  Future<void> loadItems() async {
    // Load from local DB
    notifyListeners();
  }

  Future<void> addReceipt(Map<String, dynamic> receipt) async {
    // Save to local queue
    notifyListeners();
  }
}
